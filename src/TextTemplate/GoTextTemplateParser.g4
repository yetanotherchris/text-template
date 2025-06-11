parser grammar GoTextTemplateParser;
options { tokenVocab=GoTextTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT | placeholder | ifBlock | forBlock;

placeholder : OPEN path CLOSE;

path : (DOT? IDENT) ( (DOT IDENT) | (LBRACK (NUMBER | STRING | IDENT) RBRACK) )*;

ifBlock : OPEN IF path CLOSE content (elseIfBlock)* (elseBlock)? OPEN END CLOSE;

elseIfBlock : OPEN ELSE IF path CLOSE content;

elseBlock : OPEN ELSE CLOSE content;

forBlock : OPEN FOR IDENT IN path CLOSE content (elseBlock)? OPEN END CLOSE;
