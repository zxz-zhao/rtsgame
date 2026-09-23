#ifndef LOGGER_H
#define LOGGER_H

#include <string>
#include <fstream>

namespace NetworkAccelerator {
    class Logger {
    public:
        static Logger& Instance();
        void Init(const std::string& logFilePath);
        void Log(const std::string& level, const std::string& message);
    private:
        Logger() {}
        std::ofstream logFile;
    };
}

#endif // LOGGER_H
