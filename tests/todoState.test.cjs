const {
  createTodo,
  normalizeAppState,
  removeTodo,
  toggleTodo,
  updateTodoText
} = require('../src/shared/todoState');

describe('todo state', () => {
  it('creates a trimmed active todo with deterministic metadata', () => {
    const todo = createTodo('  写周报  ', { id: 'fixed-id', now: 1710000000000 });

    expect(todo).toEqual({
      id: 'fixed-id',
      text: '写周报',
      completed: false,
      createdAt: 1710000000000,
      updatedAt: 1710000000000
    });
  });

  it('rejects blank todo text', () => {
    expect(() => createTodo('   ')).toThrow(/empty/i);
  });

  it('toggles, edits, and removes todos without mutating the input list', () => {
    const todos = [
      createTodo('First', { id: 'a', now: 1 }),
      createTodo('Second', { id: 'b', now: 2 })
    ];

    const toggled = toggleTodo(todos, 'a', 3);
    const edited = updateTodoText(toggled, 'a', '  Updated  ', 4);
    const removed = removeTodo(edited, 'b');

    expect(todos[0].completed).toBe(false);
    expect(toggled[0]).toMatchObject({ id: 'a', completed: true, updatedAt: 3 });
    expect(edited[0]).toMatchObject({ id: 'a', text: 'Updated', updatedAt: 4 });
    expect(removed.map((todo) => todo.id)).toEqual(['a']);
  });

  it('keeps existing text when editing to a blank value', () => {
    const todos = [createTodo('Keep me', { id: 'a', now: 1 })];

    expect(updateTodoText(todos, 'a', '   ', 2)).toEqual(todos);
  });

  it('normalizes persisted state with safe defaults', () => {
    const state = normalizeAppState(
      {
        todos: [
          {
            id: 'saved',
            text: '  Saved task  ',
            completed: 'yes',
            createdAt: 'bad-date'
          }
        ],
        settings: {
          alwaysOnTop: false,
          locked: true
        },
        windowBounds: {
          x: 10,
          y: 20,
          width: 100,
          height: 100
        }
      },
      { now: 5000 }
    );

    expect(state.todos[0]).toMatchObject({
      id: 'saved',
      text: 'Saved task',
      completed: true,
      createdAt: 5000,
      updatedAt: 5000
    });
    expect(state.settings).toEqual({
      alwaysOnTop: false,
      locked: true
    });
    expect(state.windowBounds).toEqual({
      x: 10,
      y: 20,
      width: 280,
      height: 360
    });
  });
});
