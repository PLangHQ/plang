Step text: `keep %users% where age > 20`
Properties: `{"List": "%users%", "Query": {"where": {"field": "age", "op": ">", "value": 20}}}` — a one-part sentence is a one-part query.

Step text: `filter %users% where type is "student" and age > 20, write to %students%`
Properties: `{"List": "%users%", "Query": {"where": {"and": [{"field": "type", "op": "==", "value": "student"}, {"field": "age", "op": ">", "value": 20}]}}}` — two conditions joined by and; the trailing `write to %students%` is its own action.

Step text: `from %users% take those under 20 or where type is "teacher", write to %some%`
Properties: `{"List": "%users%", "Query": {"where": {"or": [{"field": "age", "op": "<", "value": 20}, {"field": "type", "op": "==", "value": "teacher"}]}}}` — name the field (`type`); a bare word like "teachers" leaves the field to a guess.

Step text: `keep %users% where age > %min%, write to %old%`
Properties: `{"List": "%users%", "Query": {"where": {"field": "age", "op": ">", "value": "%min%"}}}` — a value may be a %variable%, read at run.

Step text: `group %people% by name, order by age, write to %byName%`
Properties: `{"List": "%people%", "Query": {"group": "name", "order": "age"}}` — after a group, order sorts each group's items.

Step text: `distinct %numbers%, write to %once%`
Properties: `{"List": "%numbers%", "Query": {"distinct": true}}`

Step text: `sort %users% by age, highest first, write to %oldest%`
Properties: `{"List": "%users%", "Query": {"order": {"field": "age", "desc": true}}}` — "highest/oldest/largest/newest first" is desc: true; plain "sort by age" is ascending.

Step text: `sort %people% by age`
Properties: `{"List": "%people%", "Query": {"order": "age"}}` — the step names no destination; write only this query, the builder writes the sorted answer back to %people% for you.
