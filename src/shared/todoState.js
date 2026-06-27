const crypto = require('crypto');

const MIN_WINDOW_WIDTH = 280;
const MIN_WINDOW_HEIGHT = 360;

const DEFAULT_SETTINGS = Object.freeze({
  alwaysOnTop: true,
  locked: false
});

const DEFAULT_WINDOW_BOUNDS = Object.freeze({
  width: 320,
  height: 460
});

const DEFAULT_STATE = Object.freeze({
  todos: Object.freeze([]),
  settings: DEFAULT_SETTINGS,
  windowBounds: DEFAULT_WINDOW_BOUNDS
});

function cleanText(text) {
  return String(text ?? '').trim();
}

function timestamp(value, fallback = Date.now()) {
  return Number.isFinite(value) ? value : fallback;
}

function makeId() {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function createTodo(text, options = {}) {
  const trimmed = cleanText(text);

  if (!trimmed) {
    throw new Error('Cannot create an empty todo');
  }

  const createdAt = timestamp(options.now);

  return {
    id: String(options.id ?? makeId()),
    text: trimmed,
    completed: false,
    createdAt,
    updatedAt: createdAt
  };
}

function toggleTodo(todos, id, now = Date.now()) {
  return toTodoList(todos).map((todo) => {
    if (todo.id !== id) {
      return todo;
    }

    return {
      ...todo,
      completed: !todo.completed,
      updatedAt: now
    };
  });
}

function updateTodoText(todos, id, text, now = Date.now()) {
  const trimmed = cleanText(text);

  if (!trimmed) {
    return todos;
  }

  return toTodoList(todos).map((todo) => {
    if (todo.id !== id || todo.text === trimmed) {
      return todo;
    }

    return {
      ...todo,
      text: trimmed,
      updatedAt: now
    };
  });
}

function removeTodo(todos, id) {
  return toTodoList(todos).filter((todo) => todo.id !== id);
}

function toTodoList(todos) {
  return Array.isArray(todos) ? todos : [];
}

function normalizeTodo(todo, index, options) {
  if (!todo || typeof todo !== 'object') {
    return null;
  }

  const text = cleanText(todo.text);

  if (!text) {
    return null;
  }

  const now = timestamp(options.now);
  const createdAt = timestamp(todo.createdAt, now);
  const updatedAt = timestamp(todo.updatedAt, createdAt);
  const id = cleanText(todo.id) || `todo-${index}-${createdAt}`;

  return {
    id,
    text,
    completed: Boolean(todo.completed),
    createdAt,
    updatedAt
  };
}

function sanitizeBounds(bounds = {}) {
  const safeBounds = {};

  if (Number.isFinite(bounds.x)) {
    safeBounds.x = bounds.x;
  }

  if (Number.isFinite(bounds.y)) {
    safeBounds.y = bounds.y;
  }

  safeBounds.width = Math.max(
    MIN_WINDOW_WIDTH,
    Number.isFinite(bounds.width) ? bounds.width : DEFAULT_WINDOW_BOUNDS.width
  );
  safeBounds.height = Math.max(
    MIN_WINDOW_HEIGHT,
    Number.isFinite(bounds.height) ? bounds.height : DEFAULT_WINDOW_BOUNDS.height
  );

  return safeBounds;
}

function normalizeAppState(rawState = {}, options = {}) {
  const todos = toTodoList(rawState.todos)
    .map((todo, index) => normalizeTodo(todo, index, options))
    .filter(Boolean);

  return {
    todos,
    settings: {
      alwaysOnTop:
        typeof rawState.settings?.alwaysOnTop === 'boolean'
          ? rawState.settings.alwaysOnTop
          : DEFAULT_SETTINGS.alwaysOnTop,
      locked:
        typeof rawState.settings?.locked === 'boolean'
          ? rawState.settings.locked
          : DEFAULT_SETTINGS.locked
    },
    windowBounds: sanitizeBounds(rawState.windowBounds)
  };
}

module.exports = {
  DEFAULT_SETTINGS,
  DEFAULT_STATE,
  DEFAULT_WINDOW_BOUNDS,
  MIN_WINDOW_HEIGHT,
  MIN_WINDOW_WIDTH,
  createTodo,
  normalizeAppState,
  removeTodo,
  sanitizeBounds,
  toggleTodo,
  updateTodoText
};
