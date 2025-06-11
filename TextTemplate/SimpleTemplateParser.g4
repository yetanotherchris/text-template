parser grammar SimpleTemplateParser;
options { tokenVocab=SimpleTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT | placeholder | ifBlock;

placeholder : OPEN (IDENT | DOTIDENT) CLOSE;

ifBlock : OPEN IF (IDENT | DOTIDENT) CLOSE content (OPEN ELSE CLOSE content)? OPEN END CLOSE;
