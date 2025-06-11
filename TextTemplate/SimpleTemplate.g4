grammar SimpleTemplate;

template : part* EOF;
part : TEXT | placeholder;

placeholder : OPEN IDENT CLOSE;

OPEN  : '{{' -> pushMode(EXPR);
TEXT  : (~'{'+ | '{' ~'{')+ ;

mode EXPR;
IDENT : [a-zA-Z_][a-zA-Z0-9_]*;
CLOSE : '}}' -> popMode;
WS    : [ \t\r\n]+ -> skip;
