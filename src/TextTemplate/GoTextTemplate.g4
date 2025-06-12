grammar GoTextTemplate;

template : part* EOF;
part : TEXT | placeholder | ifBlock | forBlock;

placeholder : open pipeline close;

pipeline : path (PIPE IDENT)* ;

path : (DOT? IDENT) ( (DOT IDENT) | (LBRACK (NUMBER | STRING | IDENT) RBRACK) )*;

ifBlock : open IF path close template (elseIfBlock)* (elseBlock)? open END close;
elseIfBlock : open ELSE IF path close template;
elseBlock : open ELSE close template;

forBlock : open FOR IDENT IN path close template (elseBlock)? open END close;

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
IN      : 'in';
DOT     : '.';
LBRACK  : '[';
RBRACK  : ']';
NUMBER  : [0-9]+;
STRING  : '"' (~["\\] | '\\' .)* '"';
PIPE    : '|';
IDENT   : [a-zA-Z_][a-zA-Z0-9_]*;
COMMENT : '/*' .*? '*/' -> skip;
WS      : [ \t\r\n]+ -> skip;

open  : OPEN | OPEN_TRIM;
close : CLOSE | CLOSE_TRIM;
