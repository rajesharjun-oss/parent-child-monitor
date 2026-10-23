# YouTube activity companion extension

This optional, visible Chrome/Edge extension adds YouTube watch activity to the Windows parental dashboard.

It records only:

- YouTube video ID;
- video title;
- channel name;
- cumulative playback seconds while the YouTube tab is visible; and
- the local date / last-seen time assigned by the Windows native host.

It does not record general browser history, search text, non-YouTube URLs, keystrokes, screenshots, messages, microphone audio, camera video, cookies, or credentials.

## Install on the child PC

1. Install Child Screen Time normally.
2. Open Chrome or Edge's extensions page and enable Developer mode.
3. Select **Load unpacked** and choose the installed `BrowserExtension` directory.
4. Copy the extension ID shown by the browser. The extension remains visible in the browser's extension list.
5. In PowerShell running as the same Windows user as the child's browser, run:

   ```powershell
   & "C:\Program Files\Child Screen Time\BrowserExtension\Register-NativeHost.ps1" -ExtensionId "<extension-id>" -InstallDirectory "C:\Program Files\Child Screen Time"
   ```

   If Chrome and Edge show different IDs, pass both IDs as an array.
6. Restart the browser.

For production distribution, publish the extension through the browser store or an organization-managed extension policy rather than relying on Developer mode.

## Remove the bridge

Remove the extension from Chrome/Edge, then run:

```powershell
& "C:\Program Files\Child Screen Time\BrowserExtension\Register-NativeHost.ps1" -ExtensionId "<extension-id>" -Unregister
```

The Windows agent imports native-host events into the same DPAPI-protected local data area used by the rest of Child Screen Time and applies the configured retention period.
