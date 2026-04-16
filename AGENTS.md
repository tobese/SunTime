# SunTime Project Rules

## C# Coding Conventions

- Follow Roslynator analyzer rules.
- RCS1102: Always declare classes as `static` when they contain only static members and are not intended to be instantiated.
- When generating new classes, prefer `static` by default unless the class needs instantiation (e.g., models, entities, DI-registered services).
