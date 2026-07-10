#pragma once

#include <atomic>
#include <condition_variable>
#include <cstddef>
#include <deque>
#include <fstream>
#include <mutex>
#include <string>
#include <thread>

enum class LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    Fatal
};

class AsyncLogger
{
public:
    AsyncLogger();
    ~AsyncLogger();

    bool Start(const std::string& filePath, bool echoToStderr, std::size_t maxQueueSize, std::size_t rotateBytes);
    void Log(LogLevel level, const std::string& message);
    void Stop();

private:
    std::atomic<bool> running_;
    bool echoToStderr_;
    std::size_t maxQueueSize_;
    std::size_t rotateBytes_;
    std::string filePath_;
    std::string activeDateStamp_;
    std::string activeFilePath_;
    std::thread worker_;
    std::mutex mutex_;
    std::condition_variable condition_;
    std::deque<std::string> queue_;
    std::ofstream file_;
    std::atomic<std::size_t> droppedMessages_;

    void WorkerMain();
    void RotateIfNeeded();
    bool OpenActiveFileForToday();
};

std::string LogLevelName(LogLevel level);
