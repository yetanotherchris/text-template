grammar GoTemplate;

// Parser Rules
template
    : element*
    ;

element
    : TEXT
    | action
    ;

action
    : LEFT_DELIM pipeline RIGHT_DELIM
    | LEFT_DELIM 'if' pipeline RIGHT_DELIM template (elseAction)? LEFT_DELIM 'end' RIGHT_DELIM
    | LEFT_DELIM 'range' pipeline RIGHT_DELIM template (elseAction)? LEFT_DELIM 'end' RIGHT_DELIM
    | LEFT_DELIM 'with' pipeline RIGHT_DELIM template (elseAction)? LEFT_DELIM 'end' RIGHT_DELIM
    | LEFT_DELIM 'define' STRING RIGHT_DELIM template LEFT_DELIM 'end' RIGHT_DELIM
    | LEFT_DELIM 'template' STRING (pipeline)? RIGHT_DELIM
    | LEFT_DELIM 'block' STRING pipeline RIGHT_DELIM template LEFT_DELIM 'end' RIGHT_DELIM
    ;

elseAction
    : LEFT_DELIM 'else' (pipeline)? RIGHT_DELIM template
    ;

pipeline
    : command ('|' command)*
    ;

command
    : operand+
    | operand+ (IDENTIFIER | '.') operand*
    ;

operand
    : primary ('.' IDENTIFIER '(' (pipeline (',' pipeline)*)? ')')*
    ;

primary
    : IDENTIFIER
    | chainedField
    | variable
    | functionCall
    | literal
    | '(' pipeline ')'
    ;

chainedField
    : '.' IDENTIFIER ('.' IDENTIFIER)*
    ;

variable
    : '$' IDENTIFIER
    ;


functionCall
    : IDENTIFIER '(' (pipeline (',' pipeline)*)? ')'
    ;

literal
    : STRING
    | NUMBER
    | BOOLEAN
    | 'nil'
    ;

// Lexer Rules
LEFT_DELIM
    : '{{'
    ;

RIGHT_DELIM
    : '}}'
    ;

STRING
    : '"' (~["\r\n] | '\\"')* '"'
    | '`' (~[`])* '`'
    | '\'' ( '\\' . | ~['\r\n] )* '\''
    ;

NUMBER
    : DECIMAL
    | HEXADECIMAL
    | OCTAL
    | FLOAT
    ;

fragment DECIMAL
    : [0-9]+
    ;

fragment HEXADECIMAL
    : '0' [xX] [0-9a-fA-F]+
    ;

fragment OCTAL
    : '0' [0-7]+
    ;

fragment FLOAT
    : [0-9]+ '.' [0-9]+
    | [0-9]+ ('.' [0-9]+)? [eE] [+-]? [0-9]+
    | '.' [0-9]+ ([eE] [+-]? [0-9]+)?
    ;

BOOLEAN
    : 'true'
    | 'false'
    ;

IDENTIFIER
    : [a-zA-Z_][a-zA-Z0-9_]*
    ;

TEXT
    : (~['{'] | '{' ~['{'])+
    ;

WS
    : [ \t\r\n]+ -> skip
    ;

// Comments within template actions
COMMENT
    : '/*' .*? '*/' -> skip
    ;
