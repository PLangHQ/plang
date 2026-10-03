Title — the window's title-bar text · say: screen '<title>' · builder: the quoted name right after "open screen"
Width — the drawing area's width in pixels · say: size <width>x<height> · builder: the first number of the size
Height — the drawing area's height in pixels · say: size <width>x<height> · builder: the second number of the size
OnInput — a goal called for each input event, the event an input value in %!data% · say: on input call <Goal>
OnClose — a goal called when the window is closed · say: on close call <Goal>
ToOutput — PlangOS (Linux): send the frames to this app's own output instead of to a window · say: frames to output
OnWindow — PlangOS (Linux): a goal called as a window on the screen opens, is focused, moved, closed, or asks to navigate · say: on window call <Goal>
Returns — the screen, held so later steps draw into it (draw … on %screen%).
