lexer grammar GoTextTemplateLexer;

OPEN  : '{{' -> pushMode(EXPR);
TEXT  : (~'{' | '{' ~'{')+ ;

mode EXPR;
CLOSE : '}}' -> popMode;
IF    : 'if';
ELSE  : 'else';
END   : 'end';
FOR   : 'for';
IN    : 'in';
IDENT : [a-zA-Z_][a-zA-Z0-9_]*;
DOTIDENT : '.' IDENT;
WS    : [ \t\r\n]+ -> skip;
