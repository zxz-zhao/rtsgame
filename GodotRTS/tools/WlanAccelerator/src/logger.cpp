#include "logger.h"
#include <iostream>
#include <chrono>
#include <iomanip>
#include <sstream>

namespace NetworkAccelerator {

    Logger& Logger::Instance() {
        static Logger instance;
        return instance;
    }

    void Logger::Init(const std::string& logFilePath) {
        logFile.open(logFilePath, std::ios::out | std::ios::app);
    }

    void Logger::Log(const std::string& level, const std::string& message) {
        auto now = std::chrono::system_clock::now();
        auto in_time_t = std::chrono::system_clock::to_time_t(now);

        char timeBuf[64];
        std::strftime(timeBuf, sizeof(timeBuf), "%Y-%m-%d %H:%M:%S", std::localtime(&in_time_t));

        std::stringstream ss;
        ss << timeBuf << " [" << level << "] " << message;

        std::string formatted = ss.str();
        std::cout << formatted << std::endl;

        if (logFile.is_open()) {
            logFile << formatted << std::endl;
            logFile.flush();
        }
    }
}
