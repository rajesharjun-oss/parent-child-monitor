# Child Screen Time - Open Source Parental Control

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Android](https://img.shields.io/badge/platform-Android-green.svg)
![API Level](https://img.shields.io/badge/API-28+-orange.svg)
![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)

A powerful, open source parental control application for Android that provides uncompromising screen time management and digital wellness tools for families.

> **Windows PC version:** A separate Windows 10/11 application with a boot-time Windows service, automatic agent recovery, installer, protected parent dashboard, application-usage summaries, limits, schedules, and privacy controls is available under [`pc/`](pc/README.md).

## 🌟 Features

### Core Functionality
- **Unescapable Screen Blocking**: Creates fullscreen overlay that cannot be bypassed when time limits are reached
- **Precise Usage Tracking**: Real-time monitoring with minute-by-minute accuracy
- **Credit-Based Time Management**: Flexible daily allowances with extension options
- **Password-Protected Admin**: Secure settings access (default: 3443)
- **Hardware Key Blocking**: Disables home, back, and recent apps during blocking
- **Intelligent Media Pausing**: Automatically pauses videos, games, and music

### Advanced Features
- **Foreground Service Architecture**: Persistent monitoring that survives system kills
- **WorkManager Integration**: Background resilience with adaptive intervals
- **Battery Optimized**: Intelligent monitoring frequency based on remaining time
- **Device Administrator**: Enhanced security and tamper resistance
- **Open Source**: Complete transparency with no hidden data collection

## 🚀 Quick Start

### Prerequisites
- Android 9.0 (API level 28) or higher
- Android Studio Arctic Fox or later
- Gradle 8.0+

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/childscreentime/cst.git
   cd cst
   ```

2. **Build the project**
   ```bash
   ./gradlew assembleDebug
   ```

3. **Install on device**
   ```bash
   adb install app/build/outputs/apk/debug/app-debug.apk
   ```

### First Time Setup

1. **Grant Required Permissions**
   - **Usage Access (CRITICAL)**: Settings → Special access → Usage access → Child Screen Time → Enable
     - *App will not function without this permission and will exit if denied*
   - System Alert Window (Settings → Special access → Display over other apps)
   - Device Administrator (Settings → Security → Device admin apps)

2. **Configure Time Limits**
   - Open the app and set daily time allowances
   - Configure extension credits (1-minute and 5-minute options)
   - Test the blocking functionality

3. **Secure Admin Access**
   - Default admin password is `3443`
   - Change this in production deployments

## 📱 Architecture

### Core Components

```
├── core/
│   ├── ScreenTimeApplication.java    # Application class and initialization
│   ├── TimeManager.java             # Time tracking and blocking logic
│   └── NotificationHelper.java      # Notification management
├── service/
│   ├── ScreenLockService.java       # Foreground service for monitoring
│   ├── ScreenTimeWorker.java        # Background worker for resilience
│   └── MediaNotificationListener.java # Media session control
├── ui/
│   ├── activities/
│   │   ├── MainActivity.java        # Main blocking interface
│   │   └── StatusActivity.java      # Usage statistics display
│   └── fragments/
│       ├── FirstFragment.java       # Primary UI controls
│       └── SecondFragment.java      # Settings and configuration
├── model/
│   ├── Credit.java                  # Time credit data structure
│   └── CreditPreferences.java       # Persistent storage
└── utils/
    ├── Utils.java                   # Utility functions
    └── MediaControlHelper.java      # Media session helpers
```

### Key Design Patterns

- **Foreground Service**: Ensures continuous monitoring even when app is backgrounded
- **Observer Pattern**: Real-time UI updates based on time changes
- **Strategy Pattern**: Different monitoring strategies based on remaining time
- **Singleton Pattern**: Global application state management

## 🔧 Configuration

### Time Management

```java
// Set daily credit (minutes, 5-min extensions, 1-min extensions)
Credit dailyCredit = new Credit(120, 2, 5); // 2 hours, 2×5min, 5×1min extensions

// Check if time is expiring soon (within 5 minutes)
boolean expiring = credit.expiresSoon(app.duration);
```

### Service Configuration

```xml
<!-- Manifest configuration for foreground service -->
<service 
    android:name=".service.ScreenLockService"
    android:enabled="true"
    android:exported="false"
    android:foregroundServiceType="specialUse">
    <property 
        android:name="android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE"
        android:value="Screen time monitoring and parental control" />
</service>
```

### Permissions Required

```xml
<uses-permission android:name="android.permission.SYSTEM_ALERT_WINDOW"/>
<uses-permission android:name="android.permission.PACKAGE_USAGE_STATS"/>
<uses-permission android:name="android.permission.FOREGROUND_SERVICE"/>
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_SPECIAL_USE"/>
<uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED"/>
<uses-permission android:name="android.permission.SCHEDULE_EXACT_ALARM"/>
```

## 🛠️ Development

### Building from Source

1. **Setup Development Environment**
   ```bash
   # Install Android SDK and tools
   # Set ANDROID_HOME environment variable
   export ANDROID_HOME=/path/to/android-sdk
   ```

2. **Build Debug Version**
   ```bash
   ./gradlew clean assembleDebug
   ```

3. **Run Tests**
   ```bash
   ./gradlew test
   ./gradlew connectedAndroidTest
   ```

4. **Build Release Version**
   ```bash
   ./gradlew assembleRelease
   ```

### Key Classes Overview

#### TimeManager.java
Central time management and blocking logic:
```java
// Update blocking state based on current usage
public static boolean updateBlockedState(Context context)

// Extend time with admin privileges  
public static void directTimeExtension(Context context, int minutes)

// Extend time using available credits
public static boolean extendTimeWithCredits(Context context, int minutes)
```

#### ScreenLockService.java
Foreground service for persistent monitoring:
```java
// Show/hide blocking overlay
private void updateBlockingOverlay()

// Bring app to foreground for media interruption
private void bringAppToForeground()

// Handle periodic time checks
private void setupPeriodicChecks()
```

### Adding New Features

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Make your changes** following the coding standards
4. **Add tests** for new functionality
5. **Update documentation**
6. **Submit a pull request**

## 🔍 Troubleshooting

### Common Issues

**App stops monitoring after some time**
- Check battery optimization settings
- Ensure foreground service permissions are granted
- Verify device administrator is enabled

**Blocking overlay doesn't appear**
- Confirm System Alert Window permission
- Check if device has overlay restrictions
- Verify service is running in foreground

**Usage tracking shows 0 minutes**
- Grant Usage Access permission (required for app to function)
- Restart the app after permission grant
- If permission is denied, app will exit with error message

**App won't start or exits immediately**
- Check if Usage Access permission is granted
- Go to Settings → Special access → Usage access → Child Screen Time → Enable
- App requires this permission and cannot function without it

### Debug Logging

Enable debug logging by filtering logcat:
```bash
adb logcat | grep -E "(TimeManager|ScreenLockService|ScreenTimeWorker)"
```

### Performance Monitoring

Monitor service health:
```bash
adb shell dumpsys activity services io.github.childscreentime
```

## 📊 Testing

### Unit Tests
```bash
./gradlew test
```

### Integration Tests
```bash
./gradlew connectedAndroidTest
```

### Manual Testing Checklist

- [ ] Time tracking accuracy over extended periods
- [ ] Blocking overlay appearance and persistence
- [ ] Hardware key blocking effectiveness
- [ ] Service survival after app kill
- [ ] Battery optimization impact
- [ ] Permission handling on different Android versions

## 🚢 Deployment

### Automated Release Pipeline

The project includes automated deployment to both GitHub Releases and Amazon Appstore via GitHub Actions.



## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

### Quick Contribution Guide

1. Check existing issues or create a new one
2. Fork the repository
3. Create a feature branch
4. Make your changes with tests
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Android Open Source Project for core platform capabilities
- WorkManager library for background task management

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/childscreentime/cst/issues)
- **Discussions**: [GitHub Discussions](https://github.com/childscreentime/cst/discussions)
- **Wiki**: [Project Wiki](https://github.com/childscreentime/cst/wiki)


