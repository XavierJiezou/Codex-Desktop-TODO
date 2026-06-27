const { app, BrowserWindow, Menu, Tray, ipcMain, nativeImage, screen } = require('electron');
const fs = require('fs');
const path = require('path');

const {
  DEFAULT_STATE,
  MIN_WINDOW_HEIGHT,
  MIN_WINDOW_WIDTH,
  normalizeAppState,
  sanitizeBounds
} = require('./shared/todoState');

const STATE_FILE_NAME = 'state.json';

let appState = normalizeAppState(DEFAULT_STATE);
let mainWindow = null;
let tray = null;
let isQuitting = false;
let saveTimer = null;

function getStatePath() {
  return path.join(app.getPath('userData'), STATE_FILE_NAME);
}

function readState() {
  try {
    const raw = fs.readFileSync(getStatePath(), 'utf8');
    return normalizeAppState(JSON.parse(raw));
  } catch (error) {
    if (error.code !== 'ENOENT') {
      console.warn('Could not read persisted state:', error.message);
    }

    return normalizeAppState(DEFAULT_STATE);
  }
}

function writeState() {
  const statePath = getStatePath();
  fs.mkdirSync(path.dirname(statePath), { recursive: true });
  fs.writeFileSync(statePath, JSON.stringify(appState, null, 2), 'utf8');
}

function scheduleWrite() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(writeState, 120);

  if (typeof saveTimer.unref === 'function') {
    saveTimer.unref();
  }
}

function applyWindowSettings() {
  if (!mainWindow || mainWindow.isDestroyed()) {
    return;
  }

  mainWindow.setResizable(!appState.settings.locked);
  mainWindow.setAlwaysOnTop(Boolean(appState.settings.alwaysOnTop), 'floating');
}

function rememberWindowBounds() {
  if (!mainWindow || mainWindow.isDestroyed() || mainWindow.isMinimized()) {
    return;
  }

  appState = {
    ...appState,
    windowBounds: sanitizeBounds(mainWindow.getBounds())
  };
  scheduleWrite();
}

function ensureVisibleBounds(bounds) {
  const display =
    Number.isFinite(bounds.x) && Number.isFinite(bounds.y)
      ? screen.getDisplayMatching(bounds)
      : screen.getPrimaryDisplay();
  const workArea = display.workArea;
  const width = Math.min(bounds.width, workArea.width);
  const height = Math.min(bounds.height, workArea.height);
  const nextBounds = { ...bounds, width, height };

  if (Number.isFinite(bounds.x)) {
    nextBounds.x = Math.min(Math.max(bounds.x, workArea.x), workArea.x + workArea.width - width);
  }

  if (Number.isFinite(bounds.y)) {
    nextBounds.y = Math.min(Math.max(bounds.y, workArea.y), workArea.y + workArea.height - height);
  }

  return nextBounds;
}

function saveRendererState(patch = {}) {
  appState = normalizeAppState({
    todos: Array.isArray(patch.todos) ? patch.todos : appState.todos,
    settings: {
      ...appState.settings,
      ...(patch.settings || {})
    },
    windowBounds: appState.windowBounds
  });
  applyWindowSettings();
  scheduleWrite();
  return appState;
}

function updateSettings(patch = {}) {
  appState = normalizeAppState({
    todos: appState.todos,
    settings: {
      ...appState.settings,
      ...patch
    },
    windowBounds: appState.windowBounds
  });
  applyWindowSettings();
  scheduleWrite();
  return appState.settings;
}

function createTrayIcon() {
  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">
      <rect width="64" height="64" rx="14" fill="#f7f6ef"/>
      <path d="M19 22h27M19 32h27M19 42h18" stroke="#2f332b" stroke-width="5" stroke-linecap="round"/>
      <path d="M14 23l4 4 7-10" stroke="#6f9a64" stroke-width="4" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
    </svg>
  `;

  return nativeImage
    .createFromDataURL(`data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`)
    .resize({ width: 16, height: 16 });
}

function showWindow() {
  if (!mainWindow || mainWindow.isDestroyed()) {
    createWindow();
    return;
  }

  mainWindow.show();
  mainWindow.focus();
}

function createTray() {
  tray = new Tray(createTrayIcon());
  tray.setToolTip('桌面 TODO');
  tray.setContextMenu(
    Menu.buildFromTemplate([
      { label: '显示 TODO', click: showWindow },
      {
        label: '隐藏到托盘',
        click: () => {
          if (mainWindow && !mainWindow.isDestroyed()) {
            mainWindow.hide();
          }
        }
      },
      { type: 'separator' },
      {
        label: '退出',
        click: () => {
          isQuitting = true;
          app.quit();
        }
      }
    ])
  );
  tray.on('click', showWindow);
}

function createWindow() {
  const bounds = ensureVisibleBounds(sanitizeBounds(appState.windowBounds));
  appState = {
    ...appState,
    windowBounds: bounds
  };

  mainWindow = new BrowserWindow({
    ...bounds,
    minWidth: MIN_WINDOW_WIDTH,
    minHeight: MIN_WINDOW_HEIGHT,
    frame: false,
    transparent: true,
    hasShadow: true,
    skipTaskbar: true,
    show: false,
    resizable: !appState.settings.locked,
    alwaysOnTop: appState.settings.alwaysOnTop,
    backgroundColor: '#00000000',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  applyWindowSettings();
  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    showWindow();
  });

  mainWindow.on('move', rememberWindowBounds);
  mainWindow.on('resize', rememberWindowBounds);
  mainWindow.on('close', (event) => {
    rememberWindowBounds();
    writeState();

    if (!isQuitting) {
      event.preventDefault();
      mainWindow.hide();
    }
  });
}

function registerIpc() {
  ipcMain.handle('state:load', () => appState);
  ipcMain.handle('state:save', (_event, patch) => saveRendererState(patch));
  ipcMain.handle('settings:update', (_event, patch) => updateSettings(patch));
  ipcMain.handle('window:hide', () => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.hide();
    }
  });
  ipcMain.handle('window:quit', () => {
    isQuitting = true;
    app.quit();
  });
}

app.whenReady().then(() => {
  appState = readState();
  registerIpc();
  createWindow();
  createTray();
});

app.on('before-quit', () => {
  isQuitting = true;
  writeState();
});

app.on('activate', showWindow);

app.on('window-all-closed', () => {});
