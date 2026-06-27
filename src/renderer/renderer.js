const defaultState = {
  todos: [],
  settings: {
    alwaysOnTop: true,
    locked: false
  }
};

const elements = {
  body: document.body,
  completedCount: document.getElementById('completedCount'),
  emptyState: document.getElementById('emptyState'),
  form: document.getElementById('newTodoForm'),
  hideButton: document.getElementById('hideButton'),
  input: document.getElementById('newTodoInput'),
  lockButton: document.getElementById('lockButton'),
  pinButton: document.getElementById('pinButton'),
  quitButton: document.getElementById('quitButton'),
  saveState: document.getElementById('saveState'),
  todoCount: document.getElementById('todoCount'),
  todoList: document.getElementById('todoList')
};

const saveLabels = {
  error: '保存失败',
  saved: '已保存',
  saving: '保存中'
};

let state = structuredClone(defaultState);
let editingId = null;
let saveTimer = null;

function makeId() {
  if (crypto.randomUUID) {
    return crypto.randomUUID();
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function makeTodo(text) {
  const trimmed = text.trim();

  if (!trimmed) {
    return null;
  }

  const now = Date.now();
  return {
    id: makeId(),
    text: trimmed,
    completed: false,
    createdAt: now,
    updatedAt: now
  };
}

function setSaveStatus(label) {
  elements.saveState.textContent = saveLabels[label] ?? label;
}

function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
  svg.classList.add('icon');
  use.setAttribute('href', `#icon-${name}`);
  svg.append(use);
  return svg;
}

function requestSave() {
  clearTimeout(saveTimer);
  setSaveStatus('saving');
  saveTimer = setTimeout(async () => {
    try {
      await window.todoAPI.saveState({
        todos: state.todos,
        settings: state.settings
      });
      setSaveStatus('saved');
    } catch (error) {
      console.error(error);
      setSaveStatus('error');
    }
  }, 120);
}

function renderChrome() {
  elements.body.classList.toggle('is-locked', state.settings.locked);
  elements.todoCount.textContent = `${state.todos.length} 项`;

  elements.lockButton.classList.toggle('is-active', state.settings.locked);
  elements.lockButton.title = state.settings.locked ? '解锁位置' : '锁定位置';
  elements.lockButton.setAttribute('aria-label', elements.lockButton.title);

  elements.pinButton.classList.toggle('is-active', state.settings.alwaysOnTop);
  elements.pinButton.title = state.settings.alwaysOnTop ? '取消置顶' : '保持置顶';
  elements.pinButton.setAttribute('aria-label', elements.pinButton.title);

  const completed = state.todos.filter((todo) => todo.completed).length;
  elements.completedCount.textContent = `${completed} 已完成`;
}

function renderTodos() {
  elements.todoList.innerHTML = '';

  for (const todo of state.todos) {
    const item = document.createElement('li');
    item.className = `todo${todo.completed ? ' is-complete' : ''}`;
    item.dataset.id = todo.id;

    const check = document.createElement('input');
    check.className = 'todo-check';
    check.type = 'checkbox';
    check.checked = todo.completed;
    check.title = todo.completed ? '标记未完成' : '标记完成';
    check.setAttribute('aria-label', check.title);

    const content = editingId === todo.id ? createEditInput(todo) : createText(todo);

    const remove = document.createElement('button');
    remove.className = 'delete-button';
    remove.type = 'button';
    remove.title = '删除';
    remove.setAttribute('aria-label', '删除');
    remove.append(icon('trash'));

    item.append(check, content, remove);
    elements.todoList.append(item);
  }
}

function createText(todo) {
  const text = document.createElement('div');
  text.className = 'todo-text';
  text.textContent = todo.text;
  text.title = '双击编辑';
  return text;
}

function createEditInput(todo) {
  const input = document.createElement('input');
  input.className = 'edit-input';
  input.value = todo.text;
  input.maxLength = 120;

  let finished = false;
  const finish = (commit) => {
    if (finished) {
      return;
    }

    finished = true;
    const nextText = input.value.trim();
    editingId = null;

    if (commit && nextText) {
      state.todos = state.todos.map((item) =>
        item.id === todo.id && item.text !== nextText
          ? { ...item, text: nextText, updatedAt: Date.now() }
          : item
      );
      requestSave();
    }

    render();
  };

  input.addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      finish(true);
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      finish(false);
    }
  });
  input.addEventListener('blur', () => finish(true));

  requestAnimationFrame(() => {
    input.focus();
    input.select();
  });

  return input;
}

function render() {
  renderChrome();
  renderTodos();
}

async function updateSettings(patch) {
  state.settings = {
    ...state.settings,
    ...patch
  };
  renderChrome();

  try {
    state.settings = await window.todoAPI.updateSettings(patch);
    renderChrome();
  } catch (error) {
    console.error(error);
    setSaveStatus('error');
  }
}

elements.form.addEventListener('submit', (event) => {
  event.preventDefault();
  const todo = makeTodo(elements.input.value);

  if (!todo) {
    return;
  }

  state.todos = [todo, ...state.todos];
  elements.input.value = '';
  render();
  requestSave();
});

elements.todoList.addEventListener('change', (event) => {
  if (!event.target.classList.contains('todo-check')) {
    return;
  }

  const id = event.target.closest('.todo')?.dataset.id;
  state.todos = state.todos.map((todo) =>
    todo.id === id ? { ...todo, completed: event.target.checked, updatedAt: Date.now() } : todo
  );
  render();
  requestSave();
});

elements.todoList.addEventListener('click', (event) => {
  if (!event.target.classList.contains('delete-button')) {
    return;
  }

  const id = event.target.closest('.todo')?.dataset.id;
  state.todos = state.todos.filter((todo) => todo.id !== id);
  render();
  requestSave();
});

elements.todoList.addEventListener('dblclick', (event) => {
  if (!event.target.classList.contains('todo-text')) {
    return;
  }

  editingId = event.target.closest('.todo')?.dataset.id ?? null;
  render();
});

elements.lockButton.addEventListener('click', () => {
  updateSettings({ locked: !state.settings.locked });
});

elements.pinButton.addEventListener('click', () => {
  updateSettings({ alwaysOnTop: !state.settings.alwaysOnTop });
});

elements.hideButton.addEventListener('click', () => {
  window.todoAPI.hide();
});

elements.quitButton.addEventListener('click', () => {
  window.todoAPI.quit();
});

window.todoAPI
  .loadState()
  .then((loadedState) => {
    state = {
      ...defaultState,
      ...loadedState,
      settings: {
        ...defaultState.settings,
        ...(loadedState?.settings || {})
      }
    };
    render();
  })
  .catch((error) => {
    console.error(error);
    setSaveStatus('error');
    render();
  });
