# name → claimants (winner = ResolveType)

## bool  — wins: System.Boolean
- System.Boolean [primitive alias]
- app.type.item.bool.this [@this]

## date  — wins: System.DateOnly
- System.DateOnly [primitive alias]
- app.type.item.date.this [@this]

## datetime  — wins: System.DateTimeOffset
- System.DateTimeOffset [primitive alias]
- app.type.item.datetime.this [@this]

## dict  — wins: app.type.item.dict.this
- app.type.item.dict.this [primitive alias]
- app.type.item.dict.this [@this]

## duration  — wins: System.TimeSpan
- System.TimeSpan [primitive alias]
- app.type.item.duration.this [@this]

## guid  — wins: System.Guid
- System.Guid [primitive alias]
- app.type.item.guid.this [@this]

## list  — wins: app.type.item.list.this
- app.type.item.list.this [primitive alias]
- app.type.item.list.this [@this]

## tag  — wins: app.type.item.tag.this
- app.type.item.tag.this [primitive alias]
- app.type.item.tag.this [@this]

## text  — wins: System.String
- System.String [primitive alias]
- app.type.item.text.this [@this]

## time  — wins: System.TimeOnly
- System.TimeOnly [primitive alias]
- app.type.item.time.this [@this]

# class → name, names reported by more than one class

## action
- app.goal.step.action.modifier.this (item)
- app.goal.step.action.this (item)

## bool
- app.type.item.bool.this (item)
- System.Boolean

## call
- app.callstack.call.this
- app.variable.call.this

## channel
- app.channel.this
- app.channel.type.file.this
- app.channel.type.goal.this
- app.channel.type.http.this
- app.channel.type.message.this
- app.channel.type.noop.this
- app.channel.type.session.this
- app.channel.type.stream.this

## code
- app.module.action.code.this
- app.type.code.this (item)

## date
- app.type.item.date.this (item)
- System.DateOnly

## datetime
- app.type.item.datetime.this (item)
- System.DateTime
- System.DateTimeOffset

## duration
- app.type.item.duration.this (item)
- System.TimeSpan

## error
- app.callstack.call.error.this
- app.error.Error (item)

## guid
- app.type.item.guid.this (item)
- System.Guid

## kind
- app.type.item.kind.dict.this
- app.type.item.kind.json.this
- app.type.item.kind.list.this
- app.type.item.kind.reflection.this
- app.type.item.number.kind.biginteger.this
- app.type.item.number.kind.byte.this
- app.type.item.number.kind.decimal.this
- app.type.item.number.kind.double.this
- app.type.item.number.kind.float.this
- app.type.item.number.kind.half.this
- app.type.item.number.kind.int.this
- app.type.item.number.kind.int128.this
- app.type.item.number.kind.long.this
- app.type.item.number.kind.sbyte.this
- app.type.item.number.kind.short.this
- app.type.item.number.kind.this
- app.type.item.number.kind.uint.this
- app.type.item.number.kind.uint128.this
- app.type.item.number.kind.ulong.this
- app.type.item.number.kind.ushort.this
- app.type.kind.this

## list
- app.actor.list.this
- app.callstack.call.child.list.this
- app.event.lifecycle.binding.list.this
- app.event.list.this
- app.format.list.this
- app.goal.list.this
- app.goal.step.action.list.this (item)
- app.goal.step.action.property.list.this
- app.goal.step.list.this (item)
- app.goal.tag.list.this (item)
- app.module.list.this
- app.service.list.this
- app.test.list.this
- app.type.item.choice.list.this
- app.type.item.list.this (item)
- app.type.item.type.list.this
- app.type.kind.list.this
- app.type.list.this
- app.variable.call.list.this
- app.variable.list.this
- app.warning.list.this

## number
- app.type.item.number.this (item)
- System.Decimal
- System.Double
- System.Int32
- System.Int64
- System.Single

## path
- app.type.item.path.file.this (item)
- app.type.item.path.http.this (item)
- app.type.item.path.this (item)
- app.variable.path.this

## permission
- app.actor.permission.this
- app.type.item.permission.this (item)

## reader
- app.data.reader.this
- app.type.reader.this

## tag
- app.callstack.call.tag.this
- app.type.item.tag.this (item)

## text
- app.type.item.text.this (item)
- System.String

## time
- app.type.item.time.this (item)
- System.TimeOnly

# catalog (BuildTypeEntries) name → entries (richness)

## list<clr> — catalog winner: app.callstack.call.diff.this
- app.callstack.call.diff.this richness=1
- app.goal.step.action.property.list.this richness=1
- app.callstack.call.child.list.this richness=1
- app.warning.list.this richness=1
- app.callstack.audit.this richness=1
- app.callstack.call.error.this richness=1

## action — catalog winner: app.goal.step.action.modifier.this
- app.goal.step.action.modifier.this richness=3
- app.goal.step.action.this richness=3

## path — catalog winner: app.type.item.path.this
- app.type.item.path.this richness=1
- app.type.item.path.this richness=1
- app.type.item.path.this richness=1
