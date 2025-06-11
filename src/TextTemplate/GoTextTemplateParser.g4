parser grammar GoTextTemplateParser;
options { tokenVocab=GoTextTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT | placeholder | ifBlock | forBlock | rangeBlock;

placeholder : open path close;

path : (DOT? IDENT) ( (DOT IDENT) | (LBRACK (NUMBER | STRING | IDENT) RBRACK) )*;

expr : path
     | EQ value value;

value : path
      | NUMBER
      | STRING
      | BOOLEAN;
ifBlock : open IF expr close content (elseIfBlock)* (elseBlock)? open END close;

elseIfBlock : open ELSE IF expr close content;

elseBlock : open ELSE close content;

forBlock : open FOR IDENT IN path close content (elseBlock)? open END close;

rangeBlock
    : open RANGE rangeClause close content (elseBlock)? open END close
    ;

rangeClause
    : path
    | varList COLONEQ path
    ;

varList
    : varName (COMMA varName)?
    ;

varName
    : DOLLAR? IDENT
    ;

open  : OPEN | OPEN_TRIM;
close : CLOSE | CLOSE_TRIM;
