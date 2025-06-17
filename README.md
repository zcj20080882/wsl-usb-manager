# wsl-usb-manager

#### Description

A tool that works with usbipd-win to attach USB devices to WSL, providing a user-friendly interface.

**Features**

1. Display a list of all USB devices, which can be bound or attached to WSL using checkboxes or right-click menus.
2. Show detailed information about selected devices.
3. Automatically detect USB device plug/unplug events and refresh the device list.
4. Support attaching USB devices to WSL through a specified network card.
5. Display persisted devices and allow deletion of persisted devices.
6. Support automatic attachment: devices added to the auto-attach list will be automatically attached to WSL when inserted.
7. Support Chinese and English languages.

**Requirements:**

1. Windows 10 or above
2. .NET Framework 4.8 environment
3. WSL2 environment, with usbipd-win installed (version 4.0.0 or above required)
4. Administrative privileges are needed when binding devices


#### Software Architecture

#### Build

1. Install Visual Studio 2022, along with the following extensions:

    - License Header Manager
    - Microsoft Visual Studio Installer Projects 2022

2. Install Git Version

    ```powershell
    dotnet tool install --global GitVersion.Tool
    ```

3. Build

    ```powershell
    .\buid_installer.ps1
    ```

    After building, an installer package named `WSL-USB-Manager-vx.x.x.msi` will be generated in `Installer\Release\`, where vx.x.x is the current version number.

#### Contribution

1.  Fork the repository
2.  Create Feat_xxx branch
3.  Commit your code
4.  Create Pull Request
