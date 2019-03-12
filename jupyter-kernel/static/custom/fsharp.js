CodeMirror.defineMode('fsharp', function () {
    var words = {
        'abstract': 'keyword',
        'and': 'keyword',
        'as': 'keyword',
        'assert': 'keyword',
        'base': 'keyword',
        'begin': 'keyword',
        'class': 'keyword',
        'default': 'keyword',
        'delegate': 'keyword',
        'do': 'keyword',
        'done': 'keyword',
        'downcast': 'keyword',
        'downto': 'keyword',
        'elif': 'keyword',
        'else': 'keyword',
        'end': 'keyword',
        'exception': 'keyword',
        'extern': 'keyword',
        'false': 'keyword',
        'finally': 'keyword',
        'for': 'keyword',
        'fun': 'keyword',
        'function': 'keyword',
        'global': 'keyword',
        'if': 'keyword',
        'in': 'keyword',
        'inherit': 'keyword',
        'inline': 'keyword',
        'interface': 'keyword',
        'internal': 'keyword',
        'lazy': 'keyword',
        'let': 'keyword',
        'let!': 'keyword',
        'match': 'keyword',
        'member': 'keyword',
        'module': 'keyword',
        'mutable': 'keyword',
        'namespace': 'keyword',
        'new': 'keyword',
        'not': 'keyword',
        'null': 'keyword',
        'of': 'keyword',
        'open': 'keyword',
        'or': 'keyword',
        'override': 'keyword',
        'private': 'keyword',
        'public': 'keyword',
        'rec': 'keyword',
        'return': 'keyword',
        'return!': 'keyword',
        'select': 'keyword',
        'static': 'keyword',
        'struct': 'keyword',
        'then': 'keyword',
        'to': 'keyword',
        'true': 'keyword',
        'try': 'keyword',
        'type': 'keyword',
        'upcast': 'keyword',
        'use': 'keyword',
        'use!': 'keyword',
        'val': 'keyword',
        'void': 'keyword',
        'when': 'keyword',
        'while': 'keyword',
        'with': 'keyword',
        'yield': 'keyword',
        'yield!': 'keyword',
        '__SOURCE_DIRECTORY__': 'keyword',
        'asr': 'keyword',
        'land': 'keyword',
        'lor': 'keyword',
        'lsl': 'keyword',
        'lsr': 'keyword',
        'lxor': 'keyword',
        'mod': 'keyword',
        'sig': 'keyword',
        'atomic': 'keyword',
        'break': 'keyword',
        'checked': 'keyword',
        'component': 'keyword',
        'const': 'keyword',
        'constraint': 'keyword',
        'constructor': 'keyword',
        'continue': 'keyword',
        'eager': 'keyword',
        'event': 'keyword',
        'external': 'keyword',
        'fixed': 'keyword',
        'functor': 'keyword',
        'include': 'keyword',
        'method': 'keyword',
        'mixin': 'keyword',
        'object': 'keyword',
        'parallel': 'keyword',
        'process': 'keyword',
        'protected': 'keyword',
        'pure': 'keyword',
        'sealed': 'keyword',
        'tailcall': 'keyword',
        'trait': 'keyword',
        'virtual': 'keyword',
        'volatile': 'keyword'
    };

    function tokenBase(stream, state) {
        var ch = stream.next();

        if (ch === '"') {
            state.tokenize = tokenString;
            return state.tokenize(stream, state);
        }
        if (ch === '/') {
            if (stream.eat('/')) {
                stream.skipToEnd();
                return 'comment';
            }
        }
        if (ch === '(') {
            if (stream.eat('*')) {
                state.commentLevel++;
                state.tokenize = tokenComment;
                return state.tokenize(stream, state);
            }
        }
        if (ch === '~') {
            stream.eatWhile(/\w/);
            return 'variable-2';
        }
        if (ch === '`') {
            stream.eatWhile(/\w/);
            return 'quote';
        }
        if (/\d/.test(ch)) {
            stream.eatWhile(/[\d]/);
            if (stream.eat('.')) {
                stream.eatWhile(/[\d]/);
            }
            return 'number';
        }
        if (/[+\-*&%=<>!?|]/.test(ch)) {
            return 'operator';
        }
        stream.eatWhile(/\w/);
        var cur = stream.current();
        return words[cur] || 'variable';
    }

    function tokenString(stream, state) {
        var next, end = false, escaped = false;
        while ((next = stream.next()) != null) {
            if (next === '"' && !escaped) {
                end = true;
                break;
            }
            escaped = !escaped && next === '\\';
        }
        if (end && !escaped) {
            state.tokenize = tokenBase;
        }
        return 'string';
    }

    function tokenComment(stream, state) {
        var prev, next;
        while (state.commentLevel > 0 && (next = stream.next()) != null) {
            if (prev === '(' && next === '*') state.commentLevel++;
            if (prev === '*' && next === ')') state.commentLevel--;
            prev = next;
        }
        if (state.commentLevel <= 0) {
            state.tokenize = tokenBase;
        }
        return 'comment';
    }

    return {
        startState: function () { return { tokenize: tokenBase, commentLevel: 0 }; },
        token: function (stream, state) {
            if (stream.eatSpace()) return null;
            return state.tokenize(stream, state);
        },

        blockCommentStart: "(*",
        blockCommentEnd: "*)",
        lineComment: '//'
    };
});

CodeMirror.defineMIME("text/x-fsharp", "fsharp");