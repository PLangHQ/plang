Step text: `if %count% > 0, call ProcessItems`
Mapping: `condition.if Left([object] %count%), Operator([operator] >), Right([int] 0) | goal.call GoalName([goal.call] ProcessItems)`
