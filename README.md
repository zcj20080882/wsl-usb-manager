# wsl-usb-manager

#### 介绍

一款配合usbipd-win使用的用于附加USB设备到WSL中的工具，提供友好用户界面。

**功能**

1. 显示所有USB设备列表，可通过复选框或者右键菜单进行绑定设备附加设备等操作
2. 显示已选设备的详细信息
3. 支持自动检测USB设备插拔事件，自动刷新设备列表
4. 支持通过指定网卡附加USB设备到WSL
5. 显示持久化的设备，可删除持久化设备
6. 支持自动附件功能：设备添加到自动附加列表后，当设备插入时自动附加到WSL
7. 支持中英文

**运行需求：**

1. Win10以上版本
2. .Net Framework 4.8环境
2. WSL2环境，需要安装usbipd-win（须为4.0.0或者以上版本）
3. 绑定设备时需要管理员权限

#### 软件架构


#### 构建

1.  安装Visual Studio 2022，同时安装如下扩展：

    - License Header Manager
    - Microsoft Visual Studio Installer Projects 2022

2.  安装Git Version

    ```powershell
    dotnet tool install --global GitVersion.Tool
    ```

3.  构建

    ```powershell
    .\buid_installer.ps1
    ```

    构建完成后，会在`Installer\Release\`生成一个名为`WSL-USB-Manager-vx.x.x.msi`的安装包，其中vx.x.x为当前版本号。


#### 参与贡献

1.  Fork 本仓库
2.  新建 Feat_xxx 分支
3.  提交代码
4.  新建 Pull Request
