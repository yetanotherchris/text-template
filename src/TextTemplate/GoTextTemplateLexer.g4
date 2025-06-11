lexer grammar GoTextTemplateLexer;

OPEN  : '{{' -> pushMode(EXPR);
TEXT  : (~'{' | '{' ~'{')+ ;

mode EXPR;
CLOSE   : '}}' -> popMode;
IF      : 'if';
ELSE    : 'else';
END     : 'end';
FOR     : 'for';
IN      : 'in';
DOT     : '.';
LBRACK  : '[';
RBRACK  : ']';
NUMBER  : [0-9]+;
STRING  : '"' (~["\\] | '\\' .)* '"';
IDENT   : [a-zA-Z_][a-zA-Z0-9_]*;
COMMENT : '/*' .*? '*/' -> skip;
WS      : [ \t\r\n]+ -> skip;
