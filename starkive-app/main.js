const { app, BrowserWindow, ipcMain, dialog, shell, protocol } = require('electron');
const path = require('path');
const archiver = require('archiver');
const fs = require('fs');
const Store = require('electron-store');

const store = new Store({ encryptionKey: 'starkive-local-key' });

let mainWindow;

// ── Register deep link protocol ────────────────────────────────────────────
if (process.defaultApp) {
  if (process.argv.length >= 2) {
    app.setAsDefaultProtocolClient('starkive', process.execPath, [path.resolve(process.argv[1])]);
  }
} else {
  app.setAsDefaultProtocolClient('starkive');
}

// ── Create main window ─────────────────────────────────────────────────────
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 860,
    height: 720,
    resizable: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false
    },
    icon: path.join(__dirname, 'assets', 'icon.png'),
    title: 'Starkive',
    backgroundColor: '#111318'
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  mainWindow.setMenuBarVisibility(false);

  // Send stored token on load
  mainWindow.webContents.on('did-finish-load', () => {
    const token = store.get('auth_token');
    if (token) {
      mainWindow.webContents.send('auth-token-received', token);
    }
  });
}

// ── Handle deep link (starkive://auth?token=...) ───────────────────────────
function handleDeepLink(url) {
  if (!url) return;
  try {
    const parsed = new URL(url);
    if (parsed.host === 'auth') {
      const token = parsed.searchParams.get('token');
      if (token) {
        store.set('auth_token', token);
        if (mainWindow) {
          mainWindow.webContents.send('auth-token-received', token);
        }
      }
    }
    if (parsed.host === 'upgrade') {
      if (mainWindow) {
        mainWindow.webContents.send('show-upgrade');
      }
    }
  } catch (e) {
    console.error('Deep link parse error:', e);
  }
}

// macOS — deep link via open-url event
app.on('open-url', (event, url) => {
  event.preventDefault();
  handleDeepLink(url);
});

// Windows — deep link comes in as argv
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  app.on('second-instance', (event, argv) => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.focus();
    }
    // On Windows, deep link URL is the last argv item
    const deepLink = argv.find(arg => arg.startsWith('starkive://'));
    if (deepLink) handleDeepLink(deepLink);
  });
}

// ── App lifecycle ──────────────────────────────────────────────────────────
app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

// ── IPC: zip-encrypt ───────────────────────────────────────────────────────
ipcMain.handle('zip-encrypt', async (event, { sourcePath, outputPath, password }) => {
  return new Promise((resolve) => {
    try {
      const output = fs.createWriteStream(outputPath);
      const archive = archiver.create('zip-encrypted', {
        zlib: { level: 9 },
        encryptionMethod: 'aes256',
        password
      });

      output.on('close', () => {
        resolve({ success: true, fileSizeBytes: archive.pointer() });
      });

      archive.on('error', (err) => {
        resolve({ success: false, error: err.message });
      });

      archive.pipe(output);

      const stat = fs.statSync(sourcePath);
      if (stat.isDirectory()) {
        archive.directory(sourcePath, path.basename(sourcePath));
      } else {
        archive.file(sourcePath, { name: path.basename(sourcePath) });
      }

      archive.finalize();
    } catch (err) {
      resolve({ success: false, error: err.message });
    }
  });
});

// ── IPC: open-google-auth ──────────────────────────────────────────────────
ipcMain.handle('open-google-auth', async () => {
  await shell.openExternal('http://localhost:3000/auth/google');
  return { opened: true };
});

// ── IPC: select-file ───────────────────────────────────────────────────────
ipcMain.handle('select-file', async () => {
  const result = dialog.showOpenDialogSync(mainWindow, {
    title: 'Select file or folder to encrypt',
    properties: ['openFile', 'openDirectory']
  });
  return result ? result[0] : null;
});

// ── IPC: select-output-path ────────────────────────────────────────────────
ipcMain.handle('select-output-path', async () => {
  const result = dialog.showSaveDialogSync(mainWindow, {
    title: 'Save encrypted archive as',
    defaultPath: 'archive.zip',
    filters: [{ name: 'ZIP Archive', extensions: ['zip'] }]
  });
  return result || null;
});

// ── IPC: save-credential ───────────────────────────────────────────────────
ipcMain.handle('save-credential', async (event, { credential, timestamp }) => {
  const result = dialog.showSaveDialogSync(mainWindow, {
    title: 'Save credential file',
    defaultPath: `starkive-credential-${timestamp}.txt`,
    filters: [{ name: 'Text File', extensions: ['txt'] }]
  });
  if (!result) return { saved: false };
  fs.writeFileSync(result, `Starkive Credential\nGenerated: ${timestamp}\n\n${credential}\n`);
  return { saved: true, filePath: result };
});

// ── IPC: clear-token ───────────────────────────────────────────────────────
ipcMain.handle('clear-token', async () => {
  store.delete('auth_token');
  return { cleared: true };
});
