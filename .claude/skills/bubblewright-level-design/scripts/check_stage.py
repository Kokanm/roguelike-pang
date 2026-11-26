#!/usr/bin/env python3
"""Structure check for Bubblewright StageSpec files. Python 3 standard library only.

    python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <file-or-folder> [...] [--show]

It reads each StageSpec subclass the way a person reads it: it evaluates the Rows and Ground
properties (literal strings, BlankRows(), or StageSketch.Open/Box/Lines code with loops and
grid[y][c] writes) and checks only the shape of the stage:

  * every row and the ground are 32 characters
  * Rows has 14, 28 or 42 lines and matches Storeys
  * only tile characters  . # N I S B H T  and ground characters  = N I S _
  * StageSketch.Box and grid[y][c] writes stay inside the grid
  * the class is sealed, has [Preserve], a non-empty Concept, Id, Name, World and Difficulty
  * Populate places things inside the arena (best effort; it warns when it cannot read a call)

It knows no game rule. Unlocks, gap widths, ladders, reachability, pop counts and the fair start
live in StageValidator and are checked by StageValidatorTests in Unity, so the two cannot drift.

On an error it prints the stage drawn on a 32-column ruler, row numbers on the left (row 0 is the
row standing on the ground, as in StageSketch). --show prints that drawing for every stage.
Exit code: 0 when every stage is well formed, 1 otherwise.
"""
from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path

COLS = 32
STOREY_ROWS = 14
HALF = COLS / 2
TILE_CHARS = set(".#NISBHT")
GROUND_CHARS = set("=NIS_")
TIERS = {"TierXL": 0, "TierL": 1, "TierM": 2, "TierS": 3}
TIER_NAMES = ["XL", "L", "M", "S"]


class EvalError(Exception):
    def __init__(self, message, line=None):
        super().__init__(message)
        self.line = line


# ---------------------------------------------------------------- source helpers

def strip_comments(src: str) -> str:
    """Blank out comments but keep strings, chars and line breaks so line numbers stay true."""
    out = []
    i, n = 0, len(src)
    while i < n:
        ch = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i))
            i = j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", src[i:j]))
            i = j
        elif ch == '"' or ch == "'":
            j = i + 1
            while j < n and src[j] != ch:
                j += 2 if src[j] == "\\" else 1
            out.append(src[i:j + 1])
            i = j + 1
        elif src.startswith('@"', i):
            j = i + 2
            while j < n:
                if src[j] == '"' and src.startswith('""', j):
                    j += 2
                    continue
                if src[j] == '"':
                    break
                j += 1
            out.append(src[i:j + 1])
            i = j + 1
        else:
            out.append(ch)
            i += 1
    return "".join(out)


def match_brace(src: str, open_at: int) -> int:
    """Index of the bracket closing the one at open_at. Strings and chars are skipped."""
    pairs = {"{": "}", "(": ")", "[": "]"}
    want = [pairs[src[open_at]]]
    i = open_at + 1
    while i < len(src):
        ch = src[i]
        if ch in "\"'":
            j = i + 1
            while j < len(src) and src[j] != ch:
                j += 2 if src[j] == "\\" else 1
            i = j + 1
            continue
        if ch in pairs:
            want.append(pairs[ch])
        elif want and ch == want[-1]:
            want.pop()
            if not want:
                return i
        i += 1
    raise EvalError("unbalanced brackets", None)


def line_of(src: str, index: int) -> int:
    return src.count("\n", 0, index) + 1


# ---------------------------------------------------------------- tokenizer

TOKEN_RE = re.compile(r"""
    (?P<ws>\s+)
  | (?P<num>\d+\.\d*[fFdDmM]?|\d*\.\d+[fFdDmM]?|\d+[fFdDmMuUlL]?)
  | (?P<str>@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*")
  | (?P<chr>'(?:\\.|[^'\\])')
  | (?P<id>[A-Za-z_][A-Za-z_0-9]*)
  | (?P<op>\?\?=|\?\?|==|!=|<=|>=|&&|\|\||\+\+|--|\+=|-=|\*=|/=|=>|[-+*/%<>=!?:;,.(){}\[\]])
""", re.VERBOSE)

ESCAPES = {"n": "\n", "t": "\t", "\\": "\\", "'": "'", '"': '"', "0": "\0"}


class Tok:
    __slots__ = ("kind", "text", "line")

    def __init__(self, kind, text, line):
        self.kind, self.text, self.line = kind, text, line

    def __repr__(self):
        return f"{self.kind}:{self.text}"


def tokenize(src: str, base_line: int):
    toks, pos, line = [], 0, base_line
    while pos < len(src):
        m = TOKEN_RE.match(src, pos)
        if not m:
            raise EvalError(f"cannot read '{src[pos:pos + 12]}'", line)
        kind = m.lastgroup
        text = m.group(kind)
        if kind != "ws":
            toks.append(Tok(kind, text, line))
        line += text.count("\n")
        pos = m.end()
    toks.append(Tok("eof", "", line))
    return toks


def unescape(body: str) -> str:
    out, i = [], 0
    while i < len(body):
        if body[i] == "\\" and i + 1 < len(body):
            out.append(ESCAPES.get(body[i + 1], body[i + 1]))
            i += 2
        else:
            out.append(body[i])
            i += 1
    return "".join(out)


# ---------------------------------------------------------------- tiny C# subset interpreter

class Named:
    def __init__(self, name, value):
        self.name, self.value = name, value


class Return(Exception):
    def __init__(self, value):
        self.value = value


class Grid:
    """StageSketch grid: grid[y][c], y = 0 is the row on the ground."""

    def __init__(self, storeys):
        self.rows = [["."] * COLS for _ in range(STOREY_ROWS * storeys)]


class Interp:
    def __init__(self, toks, ctx):
        self.t, self.i, self.ctx = toks, 0, ctx
        self.scopes = [dict()]

    # token helpers
    def peek(self, k=0):
        return self.t[min(self.i + k, len(self.t) - 1)]

    def take(self, text=None):
        tok = self.t[self.i]
        if text is not None and tok.text != text:
            raise EvalError(f"expected '{text}' but found '{tok.text}'", tok.line)
        self.i += 1
        return tok

    def accept(self, text):
        if self.peek().text == text:
            self.i += 1
            return True
        return False

    # variables
    def lookup(self, name, line):
        for scope in reversed(self.scopes):
            if name in scope:
                return scope[name]
        if name in self.ctx.globals:
            return self.ctx.globals[name]
        raise EvalError(f"unknown name '{name}'", line)

    def assign_var(self, name, value, line):
        for scope in reversed(self.scopes):
            if name in scope:
                scope[name] = value
                return
        raise EvalError(f"unknown variable '{name}'", line)

    # statements
    def run_block_body(self):
        while self.peek().text != "}" and self.peek().kind != "eof":
            self.statement()

    def statement(self, skip=False):
        tok = self.peek()
        if tok.text == "{":
            self.take("{")
            if skip:
                self.skip_until_close("{")
                return
            self.scopes.append({})
            try:
                self.run_block_body()
            finally:
                self.scopes.pop()
            self.take("}")
            return
        if skip:
            self.skip_statement()
            return
        if tok.text == "return":
            self.take()
            value = None if self.peek().text == ";" else self.expr()
            self.take(";")
            raise Return(value)
        if tok.text == "for":
            self.for_statement()
            return
        if tok.text == "if":
            self.if_statement()
            return
        if tok.text == ";":
            self.take()
            return
        if self.is_declaration():
            self.declaration()
            self.take(";")
            return
        self.expr()
        self.take(";")

    def skip_until_close(self, opener):
        depth = 1
        while depth:
            tok = self.take()
            if tok.kind == "eof":
                raise EvalError("unbalanced braces", tok.line)
            if tok.text == "{":
                depth += 1
            elif tok.text == "}":
                depth -= 1

    def skip_statement(self):
        tok = self.peek()
        if tok.text in ("for", "if"):
            self.take()
            self.take("(")
            depth = 1
            while depth:
                t = self.take()
                depth += {"(": 1, ")": -1}.get(t.text, 0)
            self.statement(skip=True)
            if tok.text == "if" and self.peek().text == "else":
                self.take()
                self.statement(skip=True)
            return
        depth = 0
        while True:
            t = self.take()
            if t.kind == "eof":
                return
            if t.text in "({[":
                depth += 1
            elif t.text in ")}]":
                depth -= 1
            elif t.text == ";" and depth == 0:
                return

    def is_declaration(self):
        k = 0
        tok = self.peek(k)
        if tok.kind != "id" or tok.text in ("return", "new", "true", "false", "null"):
            return False
        k += 1
        while self.peek(k).text == "[" and self.peek(k + 1).text == "]":
            k += 2
        return self.peek(k).kind == "id" and self.peek(k + 1).text in ("=", ";")

    def declaration(self):
        self.take()
        while self.accept("["):
            self.take("]")
        name = self.take().text
        value = None
        if self.accept("="):
            value = self.expr()
        self.scopes[-1][name] = value

    def for_statement(self):
        line = self.take("for").line
        self.take("(")
        self.scopes.append({})
        try:
            if self.peek().text != ";":
                if self.is_declaration():
                    self.declaration()
                else:
                    self.expr()
            self.take(";")
            cond_at = self.i
            # find the step and the body
            depth = 0
            j = self.i
            while True:
                t = self.t[j]
                if t.kind == "eof":
                    raise EvalError("bad for loop", line)
                if t.text == ";" and depth == 0:
                    break
                depth += {"(": 1, ")": -1}.get(t.text, 0)
                j += 1
            step_at = j + 1
            depth = 0
            j = step_at
            while True:
                t = self.t[j]
                if t.text == "(":
                    depth += 1
                elif t.text == ")":
                    if depth == 0:
                        break
                    depth -= 1
                j += 1
            body_at = j + 1
            guard = 0
            while True:
                self.i = cond_at
                if self.peek().text != ";" and not self.expr():
                    break
                self.i = body_at
                self.statement()
                end_at = self.i
                self.i = step_at
                if self.peek().text != ")":
                    self.expr()
                guard += 1
                if guard > 10000:
                    raise EvalError("loop runs too long", line)
            self.i = body_at
            self.statement(skip=True)
        finally:
            self.scopes.pop()

    def if_statement(self):
        self.take("if")
        self.take("(")
        cond = self.expr()
        self.take(")")
        self.statement(skip=not cond)
        if self.peek().text == "else":
            self.take()
            self.statement(skip=bool(cond))

    # expressions
    def expr(self):
        return self.assignment()

    def assignment(self):
        start = self.i
        target = self.lvalue_probe()
        if target is not None and self.peek().text in ("=", "+=", "-=", "*=", "/="):
            op = self.take().text
            value = self.assignment()
            self.store(target, op, value)
            return value
        self.i = start
        return self.ternary()

    def lvalue_probe(self):
        """Parse name or name[expr][expr] if the next tokens look like an assignment target."""
        tok = self.peek()
        if tok.kind != "id" or self.peek(1).text not in ("[", "=", "+=", "-=", "*=", "/="):
            return None
        if tok.text in ("new",):
            return None
        save = self.i
        self.take()
        indices = []
        try:
            while self.peek().text == "[":
                self.take("[")
                indices.append(self.expr())
                self.take("]")
        except EvalError:
            self.i = save
            return None
        if self.peek().text not in ("=", "+=", "-=", "*=", "/="):
            self.i = save
            return None
        return (tok.text, indices, tok.line)

    def store(self, target, op, value):
        name, indices, line = target
        if not indices:
            if op != "=":
                value = self.binop(op[0], self.lookup(name, line), value, line)
            self.assign_var(name, value, line)
            return
        container = self.lookup(name, line)
        is_grid = isinstance(container, Grid)
        if is_grid:
            container = container.rows
        for idx in indices[:-1]:
            container = self.index(container, idx, line, name)
        last = indices[-1]
        self.check_index(container, last, line, name)
        if op != "=":
            value = self.binop(op[0], container[last], value, line)
        if is_grid and len(indices) == 2:
            if value not in TILE_CHARS:
                raise EvalError(f"'{value}' is not a tile character", line)
        container[last] = value

    def check_index(self, container, idx, line, name):
        if not isinstance(idx, int):
            raise EvalError(f"index {idx!r} on {name} is not a whole number", line)
        if isinstance(container, list) and len(container) == COLS and all(isinstance(x, str) and len(x) == 1 for x in container):
            if not 0 <= idx < COLS:
                raise EvalError(f"column {idx} is outside 0..31", line)
        elif not 0 <= idx < len(container):
            raise EvalError(f"index {idx} is outside 0..{len(container) - 1} on {name}", line)

    def index(self, container, idx, line, name):
        if isinstance(container, Grid):
            container = container.rows
        if isinstance(container, (list, str)):
            self.check_index(container, idx, line, name)
            return container[idx]
        raise EvalError(f"cannot index {name}", line)

    def ternary(self):
        cond = self.null_coalesce()
        if self.accept("?"):
            a_at = self.i
            if cond:
                a = self.ternary()
                self.take(":")
                self.skip_expr()
                return a
            self.skip_expr(stop=":")
            self.take(":")
            return self.ternary()
        return cond

    def skip_expr(self, stop=None):
        depth = 0
        while True:
            t = self.peek()
            if t.kind == "eof":
                return
            if depth == 0 and (t.text in (";", ",", ")", "]", "}") or (stop and t.text == stop)):
                return
            if t.text in "([{":
                depth += 1
            elif t.text in ")]}":
                depth -= 1
            self.take()

    def null_coalesce(self):
        left = self.logic_or()
        while self.accept("??"):
            right = self.logic_or()
            left = right if left is None else left
        return left

    def logic_or(self):
        left = self.logic_and()
        while self.peek().text == "||":
            self.take()
            right = self.logic_and()
            left = bool(left) or bool(right)
        return left

    def logic_and(self):
        left = self.equality()
        while self.peek().text == "&&":
            self.take()
            right = self.equality()
            left = bool(left) and bool(right)
        return left

    def equality(self):
        left = self.relational()
        while self.peek().text in ("==", "!="):
            op = self.take().text
            right = self.relational()
            left = (left == right) if op == "==" else (left != right)
        return left

    def relational(self):
        left = self.additive()
        while self.peek().text in ("<", ">", "<=", ">="):
            op = self.take().text
            right = self.additive()
            left = {"<": left < right, ">": left > right, "<=": left <= right, ">=": left >= right}[op]
        return left

    def additive(self):
        left = self.multiplicative()
        while self.peek().text in ("+", "-"):
            tok = self.take()
            right = self.multiplicative()
            left = self.binop(tok.text, left, right, tok.line)
        return left

    def multiplicative(self):
        left = self.unary()
        while self.peek().text in ("*", "/", "%"):
            tok = self.take()
            right = self.unary()
            left = self.binop(tok.text, left, right, tok.line)
        return left

    def binop(self, op, a, b, line):
        if op == "+" and (isinstance(a, str) or isinstance(b, str)):
            return f"{a}{b}"
        if not all(isinstance(v, (int, float)) and not isinstance(v, bool) for v in (a, b)):
            raise EvalError(f"cannot apply '{op}' to {a!r} and {b!r}", line)
        if op == "+":
            return a + b
        if op == "-":
            return a - b
        if op == "*":
            return a * b
        if op == "/":
            if isinstance(a, int) and isinstance(b, int):
                return int(a / b)
            return a / b
        if op == "%":
            return int(math.fmod(a, b)) if isinstance(a, int) and isinstance(b, int) else math.fmod(a, b)
        raise EvalError(f"unknown operator {op}", line)

    def unary(self):
        tok = self.peek()
        if tok.text == "!":
            self.take()
            return not self.unary()
        if tok.text == "-":
            self.take()
            v = self.unary()
            return -v
        if tok.text == "+":
            self.take()
            return self.unary()
        if tok.text in ("++", "--"):
            self.take()
            name_tok = self.take()
            v = self.lookup(name_tok.text, name_tok.line) + (1 if tok.text == "++" else -1)
            self.assign_var(name_tok.text, v, name_tok.line)
            return v
        if tok.text == "(" and self.peek(1).text in ("int", "float") and self.peek(2).text == ")":
            self.take()
            cast = self.take().text
            self.take(")")
            v = self.unary()
            return int(v) if cast == "int" else float(v)
        return self.postfix()

    def postfix(self):
        tok = self.peek()
        value = self.primary()
        path = tok.text
        while True:
            nxt = self.peek()
            if nxt.text == ".":
                self.take()
                member = self.take()
                path = f"{path}.{member.text}"
                if self.peek().text == "(":
                    args = self.call_args()
                    value = self.ctx.call(path, value, member.text, args, member.line)
                    path = "<value>"
                else:
                    value = self.ctx.member(path, value, member.text, member.line)
            elif nxt.text == "(" and callable(value):
                args = self.call_args()
                value = value(args, nxt.line)
            elif nxt.text == "[":
                self.take()
                idx = self.expr()
                self.take("]")
                value = self.index(value, idx, nxt.line, path)
            elif nxt.text in ("++", "--") and tok.kind == "id":
                self.take()
                current = self.lookup(tok.text, tok.line)
                self.assign_var(tok.text, current + (1 if nxt.text == "++" else -1), tok.line)
                return current
            else:
                return value

    def call_args(self):
        self.take("(")
        args = []
        if self.accept(")"):
            return args
        while True:
            if self.peek().kind == "id" and self.peek(1).text == ":":
                name = self.take().text
                self.take(":")
                args.append(Named(name, self.expr()))
            else:
                args.append(self.expr())
            if self.accept(")"):
                return args
            self.take(",")

    def primary(self):
        tok = self.take()
        if tok.kind == "num":
            text = tok.text.rstrip("fFdDmMuUlL")
            if "." in text or tok.text[-1:] in "fFdDmM":
                return float(text)
            return int(text)
        if tok.kind == "str":
            if tok.text.startswith("@"):
                return tok.text[2:-1].replace('""', '"')
            return unescape(tok.text[1:-1])
        if tok.kind == "chr":
            return unescape(tok.text[1:-1])
        if tok.text == "(":
            v = self.expr()
            self.take(")")
            return v
        if tok.text == "true":
            return True
        if tok.text == "false":
            return False
        if tok.text == "null":
            return None
        if tok.text == "new":
            return self.new_expr(tok.line)
        if tok.kind == "id":
            if self.peek().text == "(":
                args = self.call_args()
                return self.ctx.call(tok.text, None, tok.text, args, tok.line)
            for scope in reversed(self.scopes):
                if tok.text in scope:
                    return scope[tok.text]
            return self.ctx.name(tok.text, tok.line)
        raise EvalError(f"unexpected '{tok.text}'", tok.line)

    def new_expr(self, line):
        if self.accept("["):
            self.take("]")
            return self.initializer()
        type_name = self.take().text
        if self.peek().text == "[" and self.peek(1).text == "]":
            self.take("[")
            self.take("]")
            if self.peek().text == "[":
                self.take("[")
                self.take("]")
            return self.initializer()
        if self.peek().text == "[":
            self.take("[")
            size = self.expr()
            self.take("]")
            if self.peek().text == "[":
                self.take("[")
                self.take("]")
            if self.peek().text == "{":
                return self.initializer()
            default = "" if type_name == "string" else ("\0" if type_name == "char" else 0)
            return [None if type_name == "string" else default for _ in range(size)]
        if type_name == "string":
            args = self.call_args()
            if len(args) == 2:
                return str(args[0]) * int(args[1])
            if len(args) == 1 and isinstance(args[0], list):
                return "".join(args[0])
            raise EvalError("new string(...) needs (char, count) or (char[])", line)
        raise EvalError(f"cannot build new {type_name}", line)

    def initializer(self):
        self.take("{")
        items = []
        if self.accept("}"):
            return items
        while True:
            items.append(self.expr())
            self.accept(",")
            if self.accept("}"):
                return items


class Context:
    """Names and calls a stage may use: StageSketch, BlankRows, SolidGround, StageMap, the builder."""

    def __init__(self, stage):
        self.stage = stage
        self.globals = {}
        self.placements = []
        self.issues = []

    def name(self, name, line):
        if name == "SolidGround":
            return "=" * COLS
        if name in TIERS:
            return TIERS[name]
        if name in self.stage.props_numeric:
            return self.stage.props_numeric[name]
        if name in ("StageSketch", "StageMap", "StageBuilder", "BubbleKind", "ClingerMount", "Mathf", "b"):
            return name
        raise EvalError(f"unknown name '{name}'", line)

    def member(self, path, value, member, line):
        if value == "StageMap":
            if member == "Cols":
                return COLS
            if member == "StoreyRows":
                return STOREY_ROWS
        if value == "StageBuilder" and member in TIERS:
            return TIERS[member]
        if value == "BubbleKind":
            return member
        if value == "ClingerMount":
            return member
        if member == "Length":
            if isinstance(value, Grid):
                return len(value.rows)
            if isinstance(value, (list, str)):
                return len(value)
        raise EvalError(f"cannot read {path}", line)

    def call(self, path, target, name, args, line):
        positional = [a for a in args if not isinstance(a, Named)]
        named = {a.name: a.value for a in args if isinstance(a, Named)}
        if path in ("StageSketch.Open",):
            storeys = (positional + [named.get("storeys", 1)])[0]
            if storeys not in (1, 2, 3):
                raise EvalError(f"StageSketch.Open({storeys}): storeys must be 1, 2 or 3", line)
            return Grid(storeys)
        if path == "StageSketch.Box":
            grid, c0, c1, y0, y1, ch = positional
            if not isinstance(grid, Grid):
                raise EvalError("StageSketch.Box needs a grid from StageSketch.Open", line)
            rows = len(grid.rows)
            for label, v, hi in (("c0", c0, COLS - 1), ("c1", c1, COLS - 1), ("y0", y0, rows - 1), ("y1", y1, rows - 1)):
                if not isinstance(v, int) or not 0 <= v <= hi:
                    raise EvalError(f"StageSketch.Box {label} = {v!r} is outside 0..{hi}", line)
            if c0 > c1 or y0 > y1:
                raise EvalError(f"StageSketch.Box({c0}, {c1}, {y0}, {y1}) is empty: give the low end first", line)
            if ch not in TILE_CHARS:
                raise EvalError(f"StageSketch.Box uses '{ch}', which is not a tile character", line)
            for y in range(y0, y1 + 1):
                for c in range(c0, c1 + 1):
                    grid.rows[y][c] = ch
            return None
        if path == "StageSketch.Lines":
            grid = positional[0]
            return ["".join(r) for r in reversed(grid.rows)]
        if path == "BlankRows":
            storeys = (positional + [named.get("storeys", 1)])[0]
            return ["." * COLS for _ in range(STOREY_ROWS * max(1, min(3, storeys)))]
        if path == "StageMap.CenterX":
            return -HALF + positional[0] + 0.5
        if path == "StageMap.Col":
            return math.floor(positional[0] + HALF)
        if path in ("Mathf.Min", "Mathf.Max"):
            return (min if name == "Min" else max)(positional)
        if name == "ToCharArray" and isinstance(target, str):
            return list(target)
        if path.startswith("b."):
            self.place(name, positional, named, line)
            return None
        raise EvalError(f"cannot evaluate {path}(...)", line)

    SIGNATURES = {
        "Wick": ["x", "y"],
        "WickAtCol": ["col", "row"],
        "Bubble": ["kind", "tier", "x", "y", "dir", "ceramic", "pogo", "steel"],
        "BubbleAt": ["kind", "tier", "col", "y", "dir", "ceramic", "pogo"],
        "Clinger": ["tier", "x", "y", "mount", "dir", "pogo", "ceramic", "patient"],
        "FloorClinger": ["tier", "x", "surfaceY", "dir", "pogo", "ceramic", "patient"],
        "SteelPogo": ["tier", "x", "surfaceY"],
        "Roller": ["x", "surfaceY", "dir", "tier", "ceramic", "steel"],
        "Spike": ["col", "width", "row", "phase"],
        "Wave": ["waveIndex", "kind", "tier", "x", "dir", "ceramic", "pogo"],
        "WaveStorey": ["storey"],
    }

    def place(self, name, positional, named, line):
        sig = self.SIGNATURES.get(name)
        if sig is None:
            raise EvalError(f"StageBuilder has no method {name}", line)
        if len(positional) > len(sig):
            raise EvalError(f"b.{name} takes at most {len(sig)} arguments", line)
        values = dict(zip(sig, positional))
        for k, v in named.items():
            if k not in sig:
                raise EvalError(f"b.{name} has no parameter '{k}'", line)
            values[k] = v
        self.placements.append((name, values, line))


# ---------------------------------------------------------------- stage parsing

class Stage:
    def __init__(self, path, src, clean, cls_name, start, end):
        self.path, self.src, self.clean = path, src, clean
        self.cls = cls_name
        self.start, self.end = start, end
        self.body = clean[start:end]
        self.props_numeric = {}
        self.errors = []
        self.warnings = []
        self.rows = None
        self.ground = None
        self.placements = []

    def err(self, line, message):
        self.errors.append((line, message))

    def warn(self, line, message):
        self.warnings.append((line, message))

    def line(self, offset_in_body):
        return line_of(self.clean, self.start + offset_in_body)

    def find_property(self, name):
        """(kind, text, line): kind 'expr' for '=> expr;' and 'block' for '{ get { ... } }'."""
        m = re.search(r"\boverride\s+[\w\[\]<>]+\s+" + name + r"\b\s*(=>|\{)", self.body)
        if not m:
            return None
        if m.group(1) == "=>":
            j = m.end()
            depth = 0
            k = j
            while k < len(self.body):
                ch = self.body[k]
                if ch in "\"'":
                    q = k + 1
                    while q < len(self.body) and self.body[q] != ch:
                        q += 2 if self.body[q] == "\\" else 1
                    k = q + 1
                    continue
                if ch in "({[":
                    depth += 1
                elif ch in ")}]":
                    depth -= 1
                elif ch == ";" and depth == 0:
                    break
                k += 1
            return ("expr", self.body[j:k], self.line(j))
        open_at = m.end() - 1
        close = match_brace(self.body, open_at)
        inner = self.body[open_at + 1:close]
        g = re.search(r"\bget\s*(=>|\{)", inner)
        if not g:
            return None
        if g.group(1) == "=>":
            k = inner.find(";", g.end())
            return ("expr", inner[g.end():k], self.line(open_at + 1 + g.end()))
        g_open = g.end() - 1
        g_close = match_brace(inner, g_open)
        return ("block", inner[g_open + 1:g_close], self.line(open_at + 1 + g_open + 1))

    def eval_property(self, name, ctx):
        found = self.find_property(name)
        if found is None:
            return None, None
        kind, text, line = found
        interp = Interp(tokenize(text, line), ctx)
        if kind == "expr":
            value = interp.expr()
            if interp.peek().kind != "eof":
                raise EvalError(f"could not read the end of {name}", interp.peek().line)
            return value, line
        try:
            interp.run_block_body()
        except Return as r:
            return r.value, line
        raise EvalError(f"{name} getter never returns", line)


def find_stages(path: Path, src: str):
    clean = strip_comments(src)
    stages = []
    for m in re.finditer(r"\bclass\s+(\w+)\s*:\s*StageSpec\b", clean):
        open_at = clean.find("{", m.end())
        if open_at < 0:
            continue
        close = match_brace(clean, open_at)
        st = Stage(path, src, clean, m.group(1), open_at + 1, close)
        head_start = max(clean.rfind("}", 0, m.start()), clean.rfind("{", 0, m.start()), clean.rfind(";", 0, m.start()))
        head = clean[head_start + 1:m.start()]
        st.head = head
        st.class_line = line_of(clean, m.start())
        stages.append(st)
    return stages


def simple_value(stage, name):
    found = stage.find_property(name)
    if found is None:
        return None, None
    kind, text, line = found
    if kind != "expr":
        return "<code>", line
    try:
        value = Interp(tokenize(text, line), Context(stage)).expr()
    except EvalError:
        return "<code>", line
    return value, line


def check_stage(stage: Stage):
    line0 = stage.class_line
    if "abstract" in stage.head:
        return False
    if "sealed" not in stage.head:
        stage.err(line0, f"{stage.cls} must be sealed: StageSpec.All only finds sealed classes.")
    if "Preserve" not in stage.head:
        stage.err(line0, f"{stage.cls} needs [Preserve] so IL2CPP keeps the class that StageSpec.All finds by reflection.")

    for prop in ("Id", "Name", "Concept"):
        value, line = simple_value(stage, prop)
        if value is None:
            stage.err(line0, f"{prop} is missing.")
        elif isinstance(value, str) and not value.strip():
            stage.err(line, f"{prop} is empty.")
    for prop in ("World", "Difficulty"):
        value, line = simple_value(stage, prop)
        if value is None:
            stage.err(line0, f"{prop} is missing.")
        elif isinstance(value, int):
            stage.props_numeric[prop] = value
    storeys, s_line = simple_value(stage, "Storeys")
    if storeys is None:
        storeys = 1
    if not isinstance(storeys, int) or storeys not in (1, 2, 3):
        stage.err(s_line or line0, f"Storeys must be 1, 2 or 3, got {storeys!r}.")
        storeys = 1
    stage.props_numeric["Storeys"] = storeys
    stage.storeys = storeys

    ctx = Context(stage)
    try:
        rows, r_line = stage.eval_property("Rows", ctx)
    except EvalError as e:
        stage.err(e.line or line0, f"Rows could not be read: {e}. Keep Rows to string literals, BlankRows() or StageSketch code.")
        rows, r_line = None, line0
    if rows is None and not any("Rows" in m for _, m in stage.errors):
        stage.err(line0, "Rows is missing.")
    if rows is not None:
        if isinstance(rows, Grid):
            rows = ["".join(r) for r in reversed(rows.rows)]
            stage.err(r_line, "Rows returns the grid itself: return StageSketch.Lines(grid).")
        if not isinstance(rows, list) or not all(isinstance(r, str) for r in rows):
            stage.err(r_line, "Rows did not produce a list of strings.")
        else:
            stage.rows = rows
            count = len(rows)
            if count not in (14, 28, 42):
                stage.err(r_line, f"Rows has {count} lines; a stage has 14, 28 or 42 (14 per storey, top row first).")
            elif count != STOREY_ROWS * storeys:
                stage.err(r_line, f"Rows has {count} lines but Storeys is {storeys} ({STOREY_ROWS * storeys} lines).")
            literal_lines = literal_row_lines(stage, r_line, count)
            for i, row in enumerate(rows):
                r = count - 1 - i
                where = literal_lines[i] if literal_lines else r_line
                if len(row) != COLS:
                    stage.err(where, f"Row {r} has {len(row)} characters, not 32: \"{row}\"")
                bad = sorted({ch for ch in row if ch not in TILE_CHARS})
                for ch in bad:
                    stage.err(where, f"Row {r} has '{ch}' at column {row.index(ch)}; tiles are . # N I S B H T.")

    try:
        ground, g_line = stage.eval_property("Ground", ctx)
    except EvalError as e:
        stage.err(e.line or line0, f"Ground could not be read: {e}.")
        ground, g_line = None, line0
    if ground is None:
        if not any("Ground" in m for _, m in stage.errors):
            stage.err(line0, "Ground is missing: use SolidGround or a 32-character string.")
    elif not isinstance(ground, str):
        stage.err(g_line, "Ground is not a string.")
    else:
        stage.ground = ground
        if len(ground) != COLS:
            stage.err(g_line, f"Ground has {len(ground)} characters, not 32: \"{ground}\"")
        for ch in sorted({c for c in ground if c not in GROUND_CHARS}):
            stage.err(g_line, f"Ground has '{ch}' at column {ground.index(ch)}; ground is = N I S _.")

    check_populate(stage)
    return True


def literal_row_lines(stage, r_line, count):
    """Source line of each literal row string, when Rows is written as literal strings."""
    found = stage.find_property("Rows")
    if not found or found[0] != "expr":
        return None
    text = found[1]
    hits = [m for m in re.finditer(r'"([^"\n]*)"', text)]
    if len(hits) != count:
        return None
    return [found[2] + text.count("\n", 0, m.start()) for m in hits]


def check_populate(stage: Stage):
    m = re.search(r"\bvoid\s+Populate\s*\(\s*StageBuilder\s+(\w+)\s*\)\s*\{", stage.body)
    if not m:
        stage.err(stage.class_line, "Populate(StageBuilder b) is missing.")
        return
    open_at = m.end() - 1
    close = match_brace(stage.body, open_at)
    text = stage.body[open_at + 1:close]
    if m.group(1) != "b":
        text = re.sub(r"\b" + m.group(1) + r"\.", "b.", text)
    ctx = Context(stage)
    interp = Interp(tokenize(text, stage.line(open_at + 1)), ctx)
    try:
        interp.run_block_body()
    except Return:
        pass
    except EvalError as e:
        stage.warn(e.line, f"Populate could not be read past here ({e}); placements after it are not drawn.")
    stage.placements = ctx.placements
    height = STOREY_ROWS * stage.storeys
    wick = 0
    for name, v, line in ctx.placements:
        x = v.get("x")
        if "col" in v and name in ("WickAtCol", "BubbleAt"):
            col = v["col"]
            if isinstance(col, int) and not 0 <= col < COLS:
                stage.err(line, f"b.{name} column {col} is outside 0..31.")
        if isinstance(x, (int, float)) and not isinstance(x, bool) and not -HALF <= x <= HALF:
            stage.err(line, f"b.{name} x = {x} is outside the arena (-16..16, 0 is the middle).")
        for key in ("y", "surfaceY"):
            y = v.get(key)
            if isinstance(y, (int, float)) and not isinstance(y, bool) and not 0 <= y <= height:
                stage.err(line, f"b.{name} {key} = {y} is outside 0..{height}.")
        row = v.get("row")
        if name == "WickAtCol" and isinstance(row, int) and not 0 <= row < height:
            stage.err(line, f"b.WickAtCol row {row} is outside 0..{height - 1}.")
        tier = v.get("tier")
        if tier is not None and isinstance(tier, int) and not 0 <= tier <= 3:
            stage.err(line, f"b.{name} tier {tier} is outside 0..3 (use StageBuilder.TierXL..TierS).")
        if name == "Spike":
            col, width = v.get("col"), v.get("width")
            if isinstance(col, int) and isinstance(width, int) and (col < 0 or width < 1 or col + width > COLS):
                stage.err(line, f"b.Spike({col}, {width}) does not fit columns 0..31.")
        if name in ("Wick", "WickAtCol"):
            wick += 1
    if wick == 0:
        stage.err(stage.class_line, "Populate never places Wick (b.Wick or b.WickAtCol).")


# ---------------------------------------------------------------- drawing

def marks(stage):
    """{(col, row): char} for placed things, drawn over the tiles."""
    out = {}
    notes = []

    def put(col, row, ch):
        if 0 <= col < COLS and 0 <= row < STOREY_ROWS * stage.storeys:
            out[(col, row)] = ch

    for name, v, line in stage.placements:
        try:
            if name == "Wick":
                col, row = math.floor(v["x"] + HALF), math.floor(v.get("y", 0))
                put(col, row, "@")
                notes.append(f"@ Wick col {col} row {row}")
            elif name == "WickAtCol":
                put(v["col"], v.get("row", 0), "@")
                notes.append(f"@ Wick col {v['col']} row {v.get('row', 0)}")
            elif name in ("Bubble", "BubbleAt", "Clinger", "FloorClinger", "SteelPogo"):
                x = v["x"] if "x" in v else -HALF + v["col"] + 0.5
                tier = v.get("tier", 0)
                if name == "FloorClinger":
                    y = v.get("surfaceY", 0) + 0.5
                elif name == "SteelPogo":
                    y = v.get("surfaceY", 0) + 0.5
                else:
                    y = v["y"]
                col, row = math.floor(x + HALF), math.floor(y)
                kind = v.get("kind", "Clinger" if "Clinger" in name else "Gloop")
                ch = {0: "X", 1: "L", 2: "M", 3: "S"}.get(tier, "?")
                if name in ("Clinger", "FloorClinger") or kind == "Clinger":
                    ch = ch.lower()
                put(col, row, ch)
                extra = []
                for flag in ("pogo", "ceramic", "steel"):
                    if v.get(flag, flag == "pogo" and name == "FloorClinger"):
                        extra.append(flag)
                if name == "SteelPogo":
                    extra.append("steel pogo")
                if name in ("Clinger", "FloorClinger"):
                    extra.append(f"mount {v.get('mount', 'Floor' if name == 'FloorClinger' else 'Ceiling')}")
                    if not v.get("patient", True):
                        extra.append("not patient")
                notes.append(f"{ch} {kind} {TIER_NAMES[tier] if isinstance(tier, int) and 0 <= tier <= 3 else tier} col {col} row {row}" + (f" ({', '.join(extra)})" if extra else ""))
            elif name == "Roller":
                col, row = math.floor(v["x"] + HALF), math.floor(v.get("surfaceY", 0))
                put(col, row, "r")
                notes.append(f"r Roller col {col} row {row}" + (" steel" if v.get("steel") else "") + (" ceramic" if v.get("ceramic") else ""))
            elif name == "Spike":
                row = v.get("row", -1)
                notes.append(f"^ Spike cols {v['col']}..{v['col'] + v['width'] - 1} " + ("on the ground" if row == -1 else f"on top of row {row}"))
                if row >= 0:
                    for c in range(v["col"], v["col"] + v["width"]):
                        put(c, row + 1, "^")
            elif name == "Wave":
                notes.append(f"  Wave {v['waveIndex']}: {v.get('kind')} {TIER_NAMES[v.get('tier', 0)]} at x {v.get('x')}")
        except (KeyError, TypeError, IndexError):
            notes.append(f"  b.{name} (not drawn)")
    return out, notes


def draw(stage) -> str:
    if not stage.rows:
        return ""
    lines = []
    ruler1 = "".join(str(c // 10) for c in range(COLS))
    ruler2 = "".join(str(c % 10) for c in range(COLS))
    lines.append(f"      {ruler1}")
    lines.append(f"      {ruler2}")
    over, notes = marks(stage)
    count = len(stage.rows)
    for i, row in enumerate(stage.rows):
        r = count - 1 - i
        drawn = "".join(over.get((c, r), ch) for c, ch in enumerate(row.ljust(COLS)[:max(COLS, len(row))]))
        flag = "" if len(row) == COLS else f"   <-- {len(row)} chars"
        lines.append(f"  {r:>2}  {drawn}{flag}")
        if r % STOREY_ROWS == 0 and r > 0:
            lines.append(f"      {'-' * COLS}  storey {r // STOREY_ROWS + 1} above, storey {r // STOREY_ROWS} below")
    if stage.ground is not None:
        flag = "" if len(stage.ground) == COLS else f"   <-- {len(stage.ground)} chars"
        lines.append(f"  gr  {stage.ground}{flag}")
    if notes:
        lines.append("      " + "\n      ".join(notes))
    return "\n".join(lines)


# ---------------------------------------------------------------- main

def collect(paths):
    files = []
    for p in paths:
        path = Path(p)
        if path.is_dir():
            files.extend(sorted(f for f in path.rglob("*.cs")))
        elif path.is_file():
            files.append(path)
        else:
            print(f"ERROR {p}: no such file or folder")
            files.append(None)
    return files


def main(argv=None):
    parser = argparse.ArgumentParser(description="Structure check for Bubblewright StageSpec files.")
    parser.add_argument("paths", nargs="+", help="StageSpec .cs files or folders to scan")
    parser.add_argument("--show", action="store_true", help="draw every stage, not only broken ones")
    args = parser.parse_args(argv)

    failed = False
    checked = 0
    for f in collect(args.paths):
        if f is None:
            failed = True
            continue
        try:
            src = f.read_text(encoding="utf-8-sig")
        except OSError as e:
            print(f"ERROR {f}: {e}")
            failed = True
            continue
        try:
            stages = find_stages(f, src)
        except EvalError as e:
            print(f"ERROR {f}: {e}")
            failed = True
            continue
        for st in stages:
            try:
                if not check_stage(st):
                    continue
            except EvalError as e:
                st.err(e.line or st.class_line, str(e))
            checked += 1
            for line, msg in sorted(st.errors, key=lambda x: x[0] or 0):
                print(f"ERROR {f}:{line}: {st.cls}: {msg}")
            for line, msg in st.warnings:
                print(f"WARN  {f}:{line}: {st.cls}: {msg}")
            if st.errors or args.show:
                picture = draw(st)
                if picture:
                    print(f"{st.cls} ({f.name}), row 0 stands on the ground:")
                    print(picture)
                    print()
            if st.errors:
                failed = True
    print(f"{'FAILED' if failed else 'OK'}: {checked} stage(s) checked. Structure only; run StageValidatorTests in Unity for the game rules.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
