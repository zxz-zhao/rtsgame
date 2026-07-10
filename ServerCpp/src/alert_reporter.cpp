#include "alert_reporter.hpp"

#include "runtime_utils.hpp"

#include <cstdlib>
#include <fstream>
#include <sstream>

namespace
{
std::string EscapeShellArg(const std::string& value)
{
#ifdef _WIN32
    return "\"" + RuntimeUtils::ReplaceAll(value, "\"", "\"\"") + "\"";
#else
    return "'" + RuntimeUtils::ReplaceAll(value, "'", "'\\''") + "'";
#endif
}
}

AlertReporter::AlertReporter(const AppConfig& config, AsyncLogger& logger)
    : config_(config)
    , logger_(logger)
{
}

void AlertReporter::Report(const std::string& title, const std::string& details)
{
    const std::string line = RuntimeUtils::CurrentTimestampLocal() + " [ALERT] " + title + " :: " + details;
    logger_.Log(LogLevel::Error, "[alert] " + title + " :: " + details);

    if (!config_.alertLogFile.empty())
    {
        const std::size_t slash = config_.alertLogFile.find_last_of("/\\");
        if (slash != std::string::npos)
            RuntimeUtils::EnsureDirectoryRecursive(config_.alertLogFile.substr(0, slash));

        std::ofstream output(config_.alertLogFile.c_str(), std::ios::app | std::ios::out);
        if (output.is_open())
            output << line << std::endl;
    }

    if (!config_.alertCommand.empty())
    {
        std::string command = config_.alertCommand;
        command = RuntimeUtils::ReplaceAll(command, "${TITLE}", EscapeShellArg(title));
        command = RuntimeUtils::ReplaceAll(command, "${MESSAGE}", EscapeShellArg(details));
        command = RuntimeUtils::ReplaceAll(command, "${LINE}", EscapeShellArg(line));
        std::system(command.c_str());
    }
}
