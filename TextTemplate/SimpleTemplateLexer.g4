lexer grammar SimpleTemplateLexer;

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
