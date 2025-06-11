parser grammar SimpleTemplateParser;
options { tokenVocab=SimpleTemplateLexer; }

template : part* EOF;
part : TEXT | placeholder;
placeholder : OPEN IDENT CLOSE;
