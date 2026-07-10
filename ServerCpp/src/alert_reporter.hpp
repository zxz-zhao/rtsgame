#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"

#include <string>

class AlertReporter
{
public:
    AlertReporter(const AppConfig& config, AsyncLogger& logger);
    void Report(const std::string& title, const std::string& details);

private:
    const AppConfig& config_;
    AsyncLogger& logger_;
};
