# VoiceNotifier

Windows 托盘语音播报程序。

## 功能

- 单实例运行，避免重复进程和重复托盘图标。
- 定时请求本地语音接口。
- `data` 为空时不播报，有效文本按顺序播报。
- 接口停止、超时或返回异常时自动重试，不终止程序。
- 托盘图标不可见时，重复启动程序可打开控制窗口并退出。

## 构建

```powershell
dotnet restore .\VoiceNotifier.csproj --runtime win-x64
dotnet publish .\VoiceNotifier.csproj --configuration Release --runtime win-x64 --self-contained true
```

配置文件为同目录下的 `config.ini`，其中包含参数说明。
