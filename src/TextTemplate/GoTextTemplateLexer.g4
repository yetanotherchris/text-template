lexer grammar GoTextTemplateLexer;

OPEN_TRIM : '{{-' -> pushMode(EXPR);
OPEN  : '{{' -> pushMode(EXPR);
TEXT  : (~'{' | '{' ~'{')+ ;

mode EXPR;
CLOSE_TRIM : '-}}' -> popMode;
CLOSE   : '}}' -> popMode;
IF      : 'if';
ELSE    : 'else';
END     : 'end';
FOR     : 'for';
RANGE   : 'range';
IN      : 'in';
COLONEQ : ':=';
COMMA   : ',';
DOLLAR  : '$';
NUMBER  : [0-9]+;
STRING  : '"' (~["\\] | '\\' .)* '"';
BOOLEAN : 'true' | 'false';
EQ      : 'eq';
NE      : 'ne';
LT      : 'lt';
LE      : 'le';
GT      : 'gt';
GE      : 'ge';
AND     : 'and';
OR      : 'or';
NOT     : 'not';
WITH    : 'with';
DEFINE  : 'define';
TEMPLATE: 'template';
BLOCK   : 'block';
PIPE    : '|';
PATH
    : ('.'? IDENT (('.' IDENT) | ('[' ~']'* ']'))+)
    | ('.' IDENT)
    ;
IDENT   : [a-zA-Z_][a-zA-Z0-9_]*;
COMMENT : '/*' .*? '*/' -> skip;
WS      : [ \t\r\n]+ -> skip;
