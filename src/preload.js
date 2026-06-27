const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('todoAPI', {
  hide: () => ipcRenderer.invoke('window:hide'),
  loadState: () => ipcRenderer.invoke('state:load'),
  quit: () => ipcRenderer.invoke('window:quit'),
  saveState: (patch) => ipcRenderer.invoke('state:save', patch),
  updateSettings: (patch) => ipcRenderer.invoke('settings:update', patch)
});
