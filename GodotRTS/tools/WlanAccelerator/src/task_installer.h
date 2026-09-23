#ifndef TASK_INSTALLER_H
#define TASK_INSTALLER_H

#include <string>

namespace NetworkAccelerator {
    bool InstallBootTask(const std::string& exePath, const std::string& taskName = "WlanAcceleratorTask");
    bool UninstallBootTask(const std::string& taskName = "WlanAcceleratorTask");
}

#endif // TASK_INSTALLER_H
