## Builder Lifecycle Documentation for Plang

Plang's builder lifecycle compiles .goal files in a set order, starting with Events, then Setup.goal, Start.goal, and others. Developers can hook into build events via EventsBuild.goal to run custom goals before/after building steps or goals, enabling tools like unit test generation and variable checks.