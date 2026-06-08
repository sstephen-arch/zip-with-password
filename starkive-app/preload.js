const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('starkive', {
  zipEncrypt: (sourcePath, outputPath, password) =>
    ipcRenderer.invoke('zip-encrypt', { sourcePath, outputPath, password }),

  openGoogleAuth: () =>
    ipcRenderer.invoke('open-google-auth'),

  selectFile: () =>
    ipcRenderer.invoke('select-file'),

  selectOutputPath: () =>
    ipcRenderer.invoke('select-output-path'),

  saveCredential: (credential, timestamp) =>
    ipcRenderer.invoke('save-credential', { credential, timestamp }),

  clearToken: () =>
    ipcRenderer.invoke('clear-token'),

  onAuthToken: (callback) => {
    ipcRenderer.on('auth-token-received', (event, token) => callback(token));
  },

  onShowUpgrade: (callback) => {
    ipcRenderer.on('show-upgrade', () => callback());
  }
});
