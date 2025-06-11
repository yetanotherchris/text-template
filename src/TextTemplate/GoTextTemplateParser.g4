parser grammar GoTextTemplateParser;
options { tokenVocab=GoTextTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT | placeholder | ifBlock | forBlock;

placeholder : OPEN (IDENT | DOTIDENT) CLOSE;

ifBlock : OPEN IF (IDENT | DOTIDENT) CLOSE content (OPEN ELSE CLOSE content)? OPEN END CLOSE;

forBlock : OPEN FOR IDENT IN (IDENT | DOTIDENT) CLOSE content OPEN END CLOSE;
