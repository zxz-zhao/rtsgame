#include "async_logger.hpp"

#include "runtime_utils.hpp"

#include <chrono>
#include <cstdio>
#include <iostream>
#include <sstream>
#include <ctime>

namespace
{
std::string FormatLogLine(LogLevel level, const std::string& message)
{
    std::ostringstream out;
    out << RuntimeUtils::CurrentTimestampLocal() << " [" << LogLevelName(level) << "] " << message;
    return out.str();
}

std::string CurrentDateStampLocal()
{
    const std::time_t now = std::time(nullptr);
    std::tm localTime;
#ifdef _WIN32
    localtime_s(&localTime, &now);
#else
    localtime_r(&now, &localTime);
#endif

    char buffer[32];
    std::strftime(buffer, sizeof(buffer), "%Y-%m-%d", &localTime);
    return buffer;
}

std::string BuildDailyLogPath(const std::string& configuredPath, const std::string& dateStamp)
{
    const std::size_t slash = configuredPath.find_last_of("/\\");
    const std::string directory = slash == std::string::npos ? std::string() : configuredPath.substr(0, slash + 1);
    const std::string fileName = slash == std::string::npos ? configuredPath : configuredPath.substr(slash + 1);

    const std::size_t dot = fileName.find_last_of('.');
    const bool hasExtension = dot != std::string::npos && dot > 0;
    const std::string stem = hasExtension ? fileName.substr(0, dot) : fileName;
    const std::string extension = hasExtension ? fileName.substr(dot) : std::string();
    return directory + stem + "-" + dateStamp + extension;
}
}

AsyncLogger::AsyncLogger()
    : running_(false)
    , echoToStderr_(true)
    , maxQueueSize_(4096)
    , rotateBytes_(8 * 1024 * 1024)
    , droppedMessages_(0)
{
}

AsyncLogger::~AsyncLogger()
{
    Stop();
}

bool AsyncLogger::Start(const std::string& filePath, bool echoToStderr, std::size_t maxQueueSize, std::size_t rotateBytes)
{
    filePath_ = filePath;
    echoToStderr_ = echoToStderr;
    maxQueueSize_ = maxQueueSize > 0 ? maxQueueSize : 4096;
    rotateBytes_ = rotateBytes > 0 ? rotateBytes : (8 * 1024 * 1024);
    activeDateStamp_.clear();
    activeFilePath_.clear();

    const std::size_t slash = filePath.find_last_of("/\\");
    if (slash != std::string::npos)
    {
        const std::string directory = filePath.substr(0, slash);
        if (!RuntimeUtils::EnsureDirectoryRecursive(directory))
            return false;
    }

    if (!OpenActiveFileForToday())
        return false;

    running_.store(true);
    worker_ = std::thread(&AsyncLogger::WorkerMain, this);
    return true;
}

void AsyncLogger::Log(LogLevel level, const std::string& message)
{
    if (!running_.load())
        return;

    const std::string line = FormatLogLine(level, message);

    {
        std::lock_guard<std::mutex> lock(mutex_);
        if (queue_.size() >= maxQueueSize_)
        {
            droppedMessages_.fetch_add(1);
            return;
        }
        queue_.push_back(line);
    }

    condition_.notify_one();
}

void AsyncLogger::Stop()
{
    const bool wasRunning = running_.exchange(false);
    if (!wasRunning)
        return;

    condition_.notify_all();
    if (worker_.joinable())
        worker_.join();

    if (file_.is_open())
        file_.close();
}

void AsyncLogger::WorkerMain()
{
    while (running_.load() || !queue_.empty())
    {
        std::deque<std::string> batch;
        {
            std::unique_lock<std::mutex> lock(mutex_);
            condition_.wait_for(lock, std::chrono::milliseconds(250), [this]()
            {
                return !queue_.empty() || !running_.load();
            });

            batch.swap(queue_);
        }

        const std::size_t dropped = droppedMessages_.exchange(0);
        if (dropped > 0)
        {
            std::ostringstream out;
            out << RuntimeUtils::CurrentTimestampLocal() << " [WARN] dropped " << dropped << " log messages because the async queue was full";
            batch.push_front(out.str());
        }

        for (std::deque<std::string>::const_iterator it = batch.begin(); it != batch.end(); ++it)
        {
            RotateIfNeeded();
            if (file_.is_open())
            {
                file_ << *it << '\n';
            }

            if (echoToStderr_)
            {
                const bool important = it->find("[ERROR]") != std::string::npos || it->find("[FATAL]") != std::string::npos;
                if (important)
                    std::cerr << *it << std::endl;
            }
        }

        if (file_.is_open())
            file_.flush();
    }
}

void AsyncLogger::RotateIfNeeded()
{
    if (!OpenActiveFileForToday() || !file_.is_open())
        return;

    const std::streampos position = file_.tellp();
    if (position < 0 || static_cast<std::size_t>(position) < rotateBytes_)
        return;

    file_.close();
    const std::string rotated = activeFilePath_ + ".1";
    std::remove(rotated.c_str());
    std::rename(activeFilePath_.c_str(), rotated.c_str());
    file_.open(activeFilePath_.c_str(), std::ios::out | std::ios::trunc);
}

bool AsyncLogger::OpenActiveFileForToday()
{
    if (filePath_.empty())
        return false;

    const std::string dateStamp = CurrentDateStampLocal();
    const std::string dailyPath = BuildDailyLogPath(filePath_, dateStamp);
    if (file_.is_open() && activeDateStamp_ == dateStamp && activeFilePath_ == dailyPath)
        return true;

    const std::size_t slash = dailyPath.find_last_of("/\\");
    if (slash != std::string::npos)
    {
        const std::string directory = dailyPath.substr(0, slash);
        if (!directory.empty() && !RuntimeUtils::EnsureDirectoryRecursive(directory))
            return false;
    }

    if (file_.is_open())
        file_.close();

    file_.open(dailyPath.c_str(), std::ios::app | std::ios::out);
    if (!file_.is_open())
        return false;

    activeDateStamp_ = dateStamp;
    activeFilePath_ = dailyPath;
    return true;
}

std::string LogLevelName(LogLevel level)
{
    switch (level)
    {
    case LogLevel::Debug: return "DEBUG";
    case LogLevel::Info: return "INFO";
    case LogLevel::Warn: return "WARN";
    case LogLevel::Error: return "ERROR";
    case LogLevel::Fatal: return "FATAL";
    }
    return "INFO";
}
