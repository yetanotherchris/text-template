grammar SimpleTemplate;

template : part* EOF;
part : TEXT | placeholder | ifBlock;

placeholder : OPEN (IDENT | DOTIDENT) CLOSE;

ifBlock : OPEN IF (IDENT | DOTIDENT) CLOSE template (OPEN ELSE CLOSE template)? OPEN END CLOSE;

OPEN  : '{{' -> pushMode(EXPR);
TEXT  : (~'{' | '{' ~'{')+ ;

mode EXPR;
CLOSE : '}}' -> popMode;
IF    : 'if';
ELSE  : 'else';
END   : 'end';
IDENT : [a-zA-Z_][a-zA-Z0-9_]*;
DOTIDENT : '.' IDENT;
WS    : [ \t\r\n]+ -> skip;
