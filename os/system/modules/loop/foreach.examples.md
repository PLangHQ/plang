Step text: `foreach %items%, call ProcessItem item=%item%`
Mapping: `loop.foreach Collection([object] %items%), ItemName([string] item) | goal.call GoalName([goal.call] ProcessItem)`
