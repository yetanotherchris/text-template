parser grammar GoTextTemplateParser;
options { tokenVocab=GoTextTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT | placeholder | ifBlock | forBlock;

placeholder : open path close;

path : (DOT? IDENT) ( (DOT IDENT) | (LBRACK (NUMBER | STRING | IDENT) RBRACK) )*;

ifBlock : open IF path close content (elseIfBlock)* (elseBlock)? open END close;

elseIfBlock : open ELSE IF path close content;

elseBlock : open ELSE close content;

forBlock : open FOR IDENT IN path close content (elseBlock)? open END close;

open  : OPEN | OPEN_TRIM;
close : CLOSE | CLOSE_TRIM;
