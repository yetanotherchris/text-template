grammar GoTextTemplate;

template : part* EOF;
part : TEXT | placeholder | ifBlock | forBlock;

placeholder : OPEN path CLOSE;

path : (DOT? IDENT) ( (DOT IDENT) | (LBRACK (NUMBER | STRING | IDENT) RBRACK) )*;

ifBlock : OPEN IF path CLOSE template (elseIfBlock)* (elseBlock)? OPEN END CLOSE;
elseIfBlock : OPEN ELSE IF path CLOSE template;
elseBlock : OPEN ELSE CLOSE template;

forBlock : OPEN FOR IDENT IN path CLOSE template (elseBlock)? OPEN END CLOSE;

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
