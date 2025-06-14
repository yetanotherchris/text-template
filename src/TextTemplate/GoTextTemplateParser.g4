parser grammar GoTextTemplateParser;
options { tokenVocab=GoTextTemplateLexer; }

template : content EOF;
content  : part*;
part     : TEXT
        | placeholder
        | ifBlock
        | forBlock
        | rangeBlock
        | withBlock
        | defineBlock
        | templateCall
        | blockBlock;

placeholder : open pipeline close;

pipeline
    : command (PIPE command)*
    ;

command
    : path
    | IDENT argument*
    ;

argument
    : path
    | NUMBER
    | STRING
    | BOOLEAN
    ;

path
    : DOLLAR? (DOT | IDENT | PATH)
    ;

expr
    : path
    | EQ value value
    | NE value value
    | LT value value
    | LE value value
    | GT value value
    | GE value value
    | AND expr expr+
    | OR expr expr+
    | NOT expr
    ;

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

withBlock
    : open WITH pipeline close content (elseBlock)? open END close
    ;

defineBlock
    : open DEFINE STRING close content open END close
    ;

templateCall
    : open TEMPLATE STRING (pipeline)? close
    ;

blockBlock
    : open BLOCK STRING pipeline close content open END close
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
