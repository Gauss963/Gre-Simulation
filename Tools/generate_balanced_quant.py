#!/usr/bin/env python3
"""Generate an original, balanced GRE Quantitative Reasoning expansion.

The scope and interaction formats follow the official Quantitative Reasoning
description in the user-authorized Official GRE Super Power Pack. No operational
or published item text is copied. Every value is calculated by the generator and
the output is deterministic.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import re
import unicodedata
from collections import Counter
from fractions import Fraction
from pathlib import Path


SOURCE = {
    "title": "Original Super Power Pack-aligned Quant item",
    "detail": "Original item based on the official GRE Quant content and format rules; no official question text copied",
    "isAuthorizedSourceItem": False,
}

# The 2026-07-20 part-two import adds 1,582 Verbal questions. Expanding the
# generated Quant resource from 1,357 to 2,939 preserves the runtime's exact
# 1:1 scored-measure balance (3,184 questions in each measure).
VARIANTS_PER_FAMILY = 31
TARGET_TOTAL = 2_939

QC_CHOICES = [
    "Quantity A is greater.",
    "Quantity B is greater.",
    "The two quantities are equal.",
    "The relationship cannot be determined from the information given.",
]


def clean_number(value: Fraction | int | float) -> str:
    value = Fraction(value)
    if value.denominator == 1:
        return str(value.numerator)
    terminating = value.denominator
    while terminating % 2 == 0:
        terminating //= 2
    while terminating % 5 == 0:
        terminating //= 5
    if terminating == 1:
        return f"{float(value):.6f}".rstrip("0").rstrip(".")
    return f"{value.numerator}/{value.denominator}"


def money(value: Fraction | int) -> str:
    return f"${float(Fraction(value)):,.2f}"


def append_constant(expression: str, constant: int) -> str:
    if constant > 0:
        return f"{expression} + {constant}"
    if constant < 0:
        return f"{expression} − {abs(constant)}"
    return expression


def linear_expression(coefficient: int, constant: int) -> str:
    if coefficient == 1:
        variable_term = "x"
    elif coefficient == -1:
        variable_term = "−x"
    else:
        variable_term = f"{coefficient}x"
    return append_constant(variable_term, constant)


def linear_function(name: str, coefficient: int, constant: int) -> str:
    return f"{name}(x) = {linear_expression(coefficient, constant)}"


def option_group(choices: list[str], maximum: int = 1) -> list[dict]:
    return [{
        "id": "main",
        "title": None,
        "options": [{"id": f"o{i}", "text": choice} for i, choice in enumerate(choices)],
        "maximumSelections": maximum,
    }]


def base(question_id: str, difficulty: str, form: str, prompt: str, explanation: str,
         area: str, *, stimulus: str | None = None, figure: dict | None = None) -> dict:
    return {
        "id": question_id,
        "measure": "quantitative",
        "difficulty": difficulty,
        "format": form,
        "stimulus": stimulus,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [],
        "correctSelections": {},
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": area,
        "source": SOURCE,
        "figure": figure,
    }


def numeric(question_id: str, difficulty: str, prompt: str, answer: Fraction | int,
            explanation: str, area: str, *, stimulus: str | None = None,
            figure: dict | None = None) -> dict:
    question = base(question_id, difficulty, "numericEntry", prompt, explanation, area,
                    stimulus=stimulus, figure=figure)
    question["acceptedNumericAnswers"] = [clean_number(answer)]
    return question


def single(question_id: str, difficulty: str, prompt: str, choices: list[str], answer: str,
           explanation: str, area: str, *, stimulus: str | None = None,
           figure: dict | None = None) -> dict:
    unique = list(dict.fromkeys(choices))
    if answer not in unique:
        unique.append(answer)
    if len(unique) < 4:
        raise ValueError(f"{question_id}: too few unique choices")
    rng = random.Random(sum((index + 1) * ord(character) for index, character in enumerate(question_id)))
    rng.shuffle(unique)
    question = base(question_id, difficulty, "singleChoice", prompt, explanation, area,
                    stimulus=stimulus, figure=figure)
    question["groups"] = option_group(unique)
    question["correctSelections"] = {"main": [f"o{unique.index(answer)}"]}
    return question


def multiple(question_id: str, difficulty: str, prompt: str, choices: list[str], correct: set[int],
             explanation: str, area: str, *, stimulus: str | None = None,
             figure: dict | None = None) -> dict:
    question = base(question_id, difficulty, "multipleChoice", prompt, explanation, area,
                    stimulus=stimulus, figure=figure)
    question["groups"] = option_group(choices, len(choices))
    question["correctSelections"] = {"main": [f"o{i}" for i in sorted(correct)]}
    return question


def comparison(question_id: str, difficulty: str, stimulus: str, quantity_a: str,
               quantity_b: str, correct: int, explanation: str, area: str,
               *, figure: dict | None = None) -> dict:
    question = base(question_id, difficulty, "quantitativeComparison",
                    "Compare Quantity A and Quantity B.", explanation, area,
                    stimulus=stimulus, figure=figure)
    question["quantityA"] = quantity_a
    question["quantityB"] = quantity_b
    question["groups"] = option_group(QC_CHOICES)
    question["correctSelections"] = {"main": [f"o{correct}"]}
    return question


def point(*, label=None, x=None, value=None, low=None, q1=None, median=None, q3=None, high=None):
    return {"label": label, "x": x, "value": value, "low": low, "q1": q1,
            "median": median, "q3": q3, "high": high}


def series(name: str, points: list[dict]) -> dict:
    return {"name": name, "points": points}


def chart(kind: str, title: str, *, caption=None, x_axis=None, y_axis=None,
          headers=None, rows=None, data=None, annotations=None) -> dict:
    return {
        "kind": kind, "title": title, "caption": caption,
        "xAxisTitle": x_axis, "yAxisTitle": y_axis,
        "headers": headers, "rows": rows, "series": data or [],
        "annotations": annotations,
    }


WORDINGS = [
    ("What is", "Determine", "Find"),
    ("How many", "Calculate the number of", "Determine how many"),
    ("Which of the following", "Select the choice that", "Identify which choice"),
]


def wording(group: int, variant: int) -> str:
    return WORDINGS[group][variant % 3]


def arithmetic(family: int, variant: int) -> list[dict]:
    p = f"bq-a{family + 1}-{variant + 1:02d}"
    if family == 0:
        cost = 80 + 20 * variant
        markup = [25, 30, 40][variant % 3]
        discount = [10, 20, 25][(variant // 3) % 3]
        marked = Fraction(cost * (100 + markup), 100)
        sale = marked * Fraction(100 - discount, 100)
        change = (sale - cost) * 100 / cost
        relation = 0 if sale > cost else 1 if sale < cost else 2
        stimulus = f"A store buys a desk lamp for ${cost}, marks it up {markup}%, and then discounts the marked price by {discount}%."
        return [
            numeric(p + "-e", "easy", f"{wording(0, variant)} the sale price, in dollars?", sale,
                    f"先加價再打折：{cost} × {100 + markup}/100 × {100 - discount}/100 = {clean_number(sale)}。", "Arithmetic", stimulus=stimulus),
            single(p + "-m", "medium", "What is the percent change from the store's cost to the sale price?",
                   [f"{clean_number(change + d)}%" for d in (-10, -5, 5, 10)] + [f"{clean_number(change)}%"], f"{clean_number(change)}%",
                   f"變動率為（售價 − 成本）÷ 成本 × 100% = {clean_number(change)}%。", "Arithmetic", stimulus=stimulus),
            comparison(p + "-h", "hard", stimulus, "The sale price", "The store's cost", relation,
                       "百分比變動必須依序相乘；計算後售價為 " + money(sale) + f"，成本為 ${cost}。", "Arithmetic"),
            numeric(p + "-x", "medium", "If the final sale price and both percentage changes are known, what was the original cost, in dollars?", cost,
                    f"反向除以兩個倍率：{clean_number(sale)} ÷ ({100 + markup}/100) ÷ ({100 - discount}/100) = {cost}。", "Arithmetic", stimulus=stimulus),
        ]
    if family == 1:
        a, b, c = [(2, 3, 5), (3, 4, 5), (4, 5, 7)][variant % 3]
        scale = 2 + variant
        total = (a + b + c) * scale
        stimulus = f"A blend contains oats, nuts, and fruit in the ratio {a}:{b}:{c}. One batch contains {total} grams."
        new_oats = a * scale + (variant % 4 + 1) * a
        new_total = new_oats * Fraction(a + b + c, a)
        relation = 0 if c > a + b else 1 if c < a + b else 2
        return [
            numeric(p + "-e", "easy", f"{wording(0, variant)} the number of grams of nuts in the batch?", b * scale,
                    f"比例共有 {a+b+c} 份；每份是 {total} ÷ {a+b+c} = {scale} 克，所以堅果為 {b*scale} 克。", "Arithmetic", stimulus=stimulus),
            numeric(p + "-m", "medium", "If a larger batch uses the same ratio and contains the stated new amount of oats, how many grams does it contain in all?", new_total,
                    f"燕麥占 {a}/{a+b+c}；總量為 {new_oats} × {a+b+c}/{a} = {clean_number(new_total)} 克。", "Arithmetic", stimulus=stimulus + f" The larger batch contains {new_oats} grams of oats."),
            multiple(p + "-h", "hard", "Select each ratio that must equal a ratio in the blend.",
                     [f"nuts : total = {b}:{a+b+c}", f"fruit : oats = {c}:{a}", f"oats : non-oats = {a}:{b+c}", f"nuts : non-nuts = {b}:{a+c}", f"fruit : total = {c}:{a+b}"],
                     {0, 1, 2, 3}, "前四項分別直接由三部分比例或其和得到；fruit : total 應為 " + f"{c}:{a+b+c}，不是 {c}:{a+b}。", "Arithmetic", stimulus=stimulus),
            comparison(p + "-x", "hard", stimulus, "The amount of fruit", "The combined amount of oats and nuts", relation,
                       f"共同倍率可約去，只需比較 {c} 與 {a}+{b}={a+b}。", "Arithmetic"),
        ]
    if family == 2:
        s1 = 36 + 4 * variant
        s2 = s1 + 12 + 2 * (variant % 4)
        hours = 2 + variant % 5
        distance = s1 * hours
        average = Fraction(2 * s1 * s2, s1 + s2)
        time2 = Fraction(distance, s2)
        saved = Fraction(hours) - time2
        stimulus = f"A vehicle travels {distance} miles at a constant speed of {s1} miles per hour."
        return [
            numeric(p + "-e", "easy", f"{wording(0, variant)} the travel time, in hours?", hours,
                    f"時間 = 距離 ÷ 速率 = {distance} ÷ {s1} = {hours} 小時。", "Arithmetic", stimulus=stimulus),
            numeric(p + "-m", "medium", f"For a round trip over the same {distance}-mile route, the outbound speed is {s1} mph and the return speed is {s2} mph. What is the average speed for the entire trip?", average,
                    f"等距往返的平均速率是 2v₁v₂/(v₁+v₂) = {clean_number(average)} mph，不能直接取兩速率平均。", "Arithmetic"),
            single(p + "-h", "hard", f"If the same {distance} miles were traveled at {s2} mph instead, how many hours would be saved?",
                   [clean_number(saved), clean_number(saved + Fraction(1,2)), clean_number(abs(Fraction(s2-s1, distance))), clean_number(Fraction(distance, s2-s1)), clean_number(Fraction(hours*s1, s2))], clean_number(saved),
                   f"原時間 {hours} 小時；新時間 {distance}/{s2}={clean_number(time2)} 小時，相差 {clean_number(saved)} 小時。", "Arithmetic"),
            numeric(p + "-x", "hard", f"A slower vehicle begins the route at {s1} mph. One hour later a faster vehicle follows at {s2} mph. How many hours after the faster vehicle starts will it catch the slower one?", Fraction(s1, s2-s1),
                    f"先行距離為 {s1} 英里，相對速率為 {s2-s1} mph；追及時間為 {s1}/{s2-s1}={clean_number(Fraction(s1,s2-s1))} 小時。", "Arithmetic"),
        ]
    if family == 3:
        prime_values = [2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31]
        primes = (
            prime_values[variant % len(prime_values)],
            prime_values[(variant + 1 + variant // len(prime_values)) % len(prime_values)],
        )
        e1, e2 = 2 + variant % 3, 1 + (variant // 3) % 3
        n = primes[0] ** e1 * primes[1] ** e2
        divisors = (e1 + 1) * (e2 + 1)
        largest_proper = n // min(primes)
        candidates = [primes[0], primes[1], primes[0] * primes[1], primes[0] ** (e1 + 1), primes[1] ** (e2 + 1)]
        correct = {i for i, value in enumerate(candidates) if n % value == 0}
        modulus = 5 + variant % 7
        return [
            numeric(p + "-e", "easy", f"The prime factorization of n is {primes[0]}^{e1} × {primes[1]}^{e2}. {wording(1, variant)} positive divisors does n have?", divisors,
                    f"各質因數指數可從 0 到原指數，因此因數個數為 ({e1}+1)({e2}+1)={divisors}。", "Arithmetic"),
            single(p + "-m", "medium", f"If n = {n}, what is its greatest positive factor less than n?",
                   [str(largest_proper), str(n // max(primes)), str(n - 1), str(n // (primes[0]*primes[1])), str(min(primes))], str(largest_proper),
                   f"最大的真因數由 n 除以最小質因數 {min(primes)} 得到：{n}÷{min(primes)}={largest_proper}。", "Arithmetic"),
            multiple(p + "-h", "hard", f"Select each number that is a divisor of {n}.", [str(x) for x in candidates], correct,
                     "檢查質因數指數不得超過 n 的分解；符合者為 " + "、".join(str(candidates[i]) for i in sorted(correct)) + "。", "Arithmetic"),
            numeric(p + "-x", "medium", f"What is the remainder when {n} is divided by {modulus}?", n % modulus,
                    f"{n} = {n//modulus}×{modulus}+{n%modulus}，所以餘數為 {n%modulus}。", "Arithmetic"),
        ]
    if family == 4:
        first = 3 + variant
        step = 2 + variant % 5
        count = 8 + variant % 7
        last = first + (count - 1) * step
        total = Fraction(count * (first + last), 2)
        limit_count = count + 3
        limit = first + (limit_count - 1) * step
        stimulus = f"The arithmetic sequence has first term {first} and common difference {step}."
        return [
            numeric(p + "-e", "easy", f"An arithmetic sequence has first term {first} and common difference {step}. What is its {count}th term?", last,
                    f"aₙ=a₁+(n−1)d={first}+({count}−1)×{step}={last}。", "Arithmetic"),
            numeric(p + "-m", "medium", f"{wording(0, variant)} the sum of the first {count} terms of that sequence?", total,
                    f"首尾平均乘以項數：{count}({first}+{last})/2={clean_number(total)}。", "Arithmetic", stimulus=stimulus),
            single(p + "-h", "hard", f"How many terms of the sequence are less than or equal to {limit}?",
                   [str(limit_count + d) for d in (-2, -1, 0, 1, 2)], str(limit_count),
                   f"解 {first}+(n−1){step}≤{limit}，得到 n≤{limit_count}，共有 {limit_count} 項。", "Arithmetic", stimulus=stimulus),
            comparison(p + "-x", "medium", stimulus + f" The first {count} terms run from {first} to {last}.", "Their arithmetic mean", f"({first} + {last})/2", 2,
                       "等差數列的平均數等於首項與末項的平均，兩量相等。", "Arithmetic"),
        ]
    total = 120 + 5 * variant
    both = 12 + variant
    only_a = 28 + variant % 9
    only_b = 34 + (2 * variant) % 11
    a_count, b_count = only_a + both, only_b + both
    union, neither = only_a + only_b + both, total - only_a - only_b - both
    stimulus = f"Among {total} students, {a_count} study French, {b_count} study German, and {both} study both languages."
    return [
        numeric(p + "-e", "easy", f"{wording(1, variant)} students study neither language?", neither,
                f"至少學一種者為 {a_count}+{b_count}−{both}={union}；兩者皆不學為 {total}−{union}={neither}。", "Arithmetic", stimulus=stimulus),
        numeric(p + "-m", "medium", "How many students study exactly one of the two languages?", only_a + only_b,
                f"只學法文 {a_count}−{both}={only_a}，只學德文 {b_count}−{both}={only_b}，合計 {only_a+only_b}。", "Arithmetic", stimulus=stimulus),
        single(p + "-h", "hard", "If one student is selected at random, what is the probability that the student studies at least one of the languages?",
               [clean_number(Fraction(union + d, total)) for d in (-both, -5, 0, 5, both)], clean_number(Fraction(union, total)),
               f"聯集人數為 {union}，所以機率為 {union}/{total}={clean_number(Fraction(union,total))}。", "Arithmetic", stimulus=stimulus),
        comparison(p + "-x", "hard", stimulus, "Students who study French only", "Students who study German only", 0 if only_a > only_b else 1 if only_a < only_b else 2,
                   f"兩量分別為 {a_count}−{both}={only_a} 與 {b_count}−{both}={only_b}。", "Arithmetic"),
    ]


def algebra(family: int, variant: int) -> list[dict]:
    p = f"bq-b{family + 1}-{variant + 1:02d}"
    if family == 0:
        x = variant - 4
        a = 3 + variant % 5
        b = 7 + 2 * variant
        c = a * x + b
        return [
            numeric(p + "-e", "easy", f"If {a}x + {b} = {c}, what is x?", x,
                    f"兩邊減 {b} 再除以 {a}：x=({c}−{b})/{a}={x}。", "Algebra"),
            numeric(p + "-m", "medium", f"If {a}x + {b} = {c}, what is the value of {2*a}x + {2*b+3}?", 2*c + 3,
                    f"{2*a}x+{2*b+3}=2({a}x+{b})+3=2×{c}+3={2*c+3}。", "Algebra"),
            comparison(p + "-h", "hard", f"{a}x + {b} = {c}", "x²", f"{abs(x)}", 0 if x*x > abs(x) else 1 if x*x < abs(x) else 2,
                       f"先解得 x={x}；因此兩量為 {x*x} 與 {abs(x)}。", "Algebra"),
            single(p + "-x", "medium", f"For what value of k does {a}(x + k) = {c} have solution x = {x}?",
                   [str((b // a) + d) for d in (-2, -1, 0, 1, 2)] + [clean_number(Fraction(b,a))], clean_number(Fraction(b,a)),
                   f"代入 x={x}：{a}({x}+k)={c}，所以 k={c}/{a}−{x}={clean_number(Fraction(b,a))}。", "Algebra"),
        ]
    if family == 1:
        x, y = 2 + variant, 5 + (variant % 7)
        a, b = 2 + variant % 3, 3 + (variant // 3) % 3
        s, t = x + y, a * x - b * y
        stimulus = f"x + y = {s} and {a}x − {b}y = {t}"
        return [
            numeric(p + "-e", "easy", "What is the value of x + y?", s, "第一個方程已直接給出 x+y。", "Algebra", stimulus=stimulus),
            numeric(p + "-m", "medium", "What is the value of x?", x,
                    f"以 y={s}−x 代入第二式，解得 x={x}，再得 y={y}。", "Algebra", stimulus=stimulus),
            single(p + "-h", "hard", f"What is the value of {b+1}x + {a+1}y?",
                   [str((b+1)*x+(a+1)*y+d) for d in (-s, -2, 0, 2, s)], str((b+1)*x+(a+1)*y),
                   f"聯立解為 x={x}, y={y}；代入得 {(b+1)*x+(a+1)*y}。", "Algebra", stimulus=stimulus),
            comparison(p + "-x", "hard", stimulus, "x", "y", 0 if x > y else 1 if x < y else 2,
                       f"聯立解得 x={x}、y={y}，直接比較即可。", "Algebra"),
        ]
    if family == 2:
        r = 1 + variant
        s = r + 2 + variant % 5
        summ, prod = r + s, r * s
        stimulus = f"x² − {summ}x + {prod} = 0"
        return [
            multiple(p + "-e", "easy", "Select each solution of the equation.", [str(r), str(s), str(-r), str(-s), str(summ)], {0, 1},
                     f"因式分解為 (x−{r})(x−{s})=0，解為 {r} 與 {s}。", "Algebra", stimulus=stimulus),
            numeric(p + "-m", "medium", "What is the sum of the squares of the two solutions?", r*r+s*s,
                    f"兩根為 {r}、{s}，平方和為 {r*r}+{s*s}={r*r+s*s}。", "Algebra", stimulus=stimulus),
            numeric(p + "-h", "hard", f"If the two solutions are increased by {variant%4+1}, what is the constant term of the monic quadratic having the new solutions?", (r+variant%4+1)*(s+variant%4+1),
                    f"新根為 {r+variant%4+1} 與 {s+variant%4+1}；首項係數為 1 時常數項為兩根乘積 {(r+variant%4+1)*(s+variant%4+1)}。", "Algebra", stimulus=stimulus),
            comparison(p + "-x", "medium", stimulus, "The product of the solutions", f"{prod}", 2,
                       "由韋達定理或直接因式分解，兩根乘積就是常數項 " + str(prod) + "。", "Algebra"),
        ]
    if family == 3:
        a, b = 2 + variant % 5, variant - 6
        c, d = 1 + (variant // 3) % 4, 4 + variant % 6
        t = 2 + variant % 7
        f_t, composition = a*t+b, a*(c*t+d)+b
        f_definition = linear_function("f", a, b)
        g_definition = linear_function("g", c, d)
        definitions = f"{f_definition} and {g_definition}"
        equality = f"{linear_expression(a, b)} = {linear_expression(c, d)}"
        return [
            numeric(p + "-e", "easy", f"If {f_definition}, what is f({t})?", f_t,
                    f"代入 x = {t}：f({t}) = {append_constant(f'{a} × {t}', b)} = {f_t}。", "Algebra"),
            numeric(p + "-m", "medium", f"If {definitions}, what is f(g({t}))?", composition,
                    f"先算 g({t})={c*t+d}，再代入 f，得到 {composition}。", "Algebra"),
            single(p + "-h", "hard", "For what value of x is f(x) = g(x)?",
                   [clean_number(Fraction(d-b, a-c)+k) for k in (-2,-1,0,1,2)] if a != c else ["0","1","2","All real numbers","No solution"],
                   clean_number(Fraction(d-b, a-c)) if a != c else ("All real numbers" if b == d else "No solution"),
                   (f"解 {equality}，得 x = {clean_number(Fraction(d-b,a-c))}。" if a != c else
                    ("兩函數的斜率與截距都相同，因此所有實數皆為解。" if b == d else "兩函數斜率相同但截距不同，因此沒有交點。")), "Algebra",
                   stimulus=f"{definitions}."),
            comparison(p + "-x", "hard", definitions, "f(g(0))", "g(f(0))", 0 if a*d+b > c*b+d else 1 if a*d+b < c*b+d else 2,
                       f"f(g(0))={a*d+b}；g(f(0))={c*b+d}。", "Algebra"),
        ]
    if family == 4:
        low = 20 + variant
        high = low + 8 + variant % 5
        integers = list(range(low + 1, high))
        options = [low-1, low, low+1, high-1, high, high+1]
        correct = {i for i, value in enumerate(options) if low < value < high}
        return [
            single(p + "-e", "easy", f"Which value satisfies x > {low}?", [str(low+d) for d in (-4,-3,-2,-1,1)], str(low+1),
                   f"嚴格不等式不含 {low}，選項中 {low+1} 大於 {low}。", "Algebra"),
            numeric(p + "-m", "medium", f"How many integers x satisfy {low} < x < {high}?", len(integers),
                    f"整數為 {low+1} 到 {high-1}，共有 {high-low-1} 個。", "Algebra"),
            multiple(p + "-h", "hard", f"Select each listed value that satisfies both x > {low} and x < {high}.", [str(x) for x in options], correct,
                     f"解集是開區間 ({low}, {high})；端點不包括。", "Algebra"),
            comparison(p + "-x", "medium", f"x is an integer and {low} < x < {high}.", "The greatest possible value of x", f"{high} − 1", 2,
                       f"最大的整數是 {high-1}，正好等於 {high}−1。", "Algebra"),
        ]
    x1, y1 = variant - 3, 2 * variant + 1
    slope = [-3, -2, 1, 2, 3][variant % 5]
    intercept = y1 - slope * x1
    x2 = x1 + 4
    y2 = slope * x2 + intercept
    other_slope = slope + (2 if slope < 2 else -2)
    other_intercept = intercept + 6
    ix = Fraction(other_intercept-intercept, slope-other_slope)
    iy = slope*ix+intercept
    stimulus = f"Line L passes through ({x1}, {y1}) and ({x2}, {y2})."
    return [
        numeric(p + "-e", "easy", "What is the slope of line L?", slope,
                f"斜率 = ({y2}−{y1})/({x2}−{x1})={slope}。", "Algebra", stimulus=stimulus),
        numeric(p + "-m", "medium", "What is the y-intercept of line L?", intercept,
                f"代入 y=mx+b：{y1}={slope}×{x1}+b，得 b={intercept}。", "Algebra", stimulus=stimulus),
        single(p + "-h", "hard", f"Line M has equation y = {other_slope}x + {other_intercept}. What is the x-coordinate of the intersection of L and M?",
               [clean_number(ix+d) for d in (-2,-1,0,1,2)], clean_number(ix),
               f"令 {slope}x+{intercept}={other_slope}x+{other_intercept}，得 x={clean_number(ix)}。", "Algebra", stimulus=stimulus),
        numeric(p + "-x", "hard", "What is the y-coordinate of that intersection?", iy,
                f"將 x={clean_number(ix)} 代入任一條直線，得 y={clean_number(iy)}。", "Algebra", stimulus=stimulus + f" Line M is y = {other_slope}x + {other_intercept}."),
    ]


def geometry(family: int, variant: int) -> list[dict]:
    p = f"bq-g{family + 1}-{variant + 1:02d}"
    if family == 0:
        length, width = 12 + variant, 5 + variant % 8
        area, perimeter = length*width, 2*(length+width)
        border = 1 + variant % 4
        border_area = (length+2*border)*(width+2*border)-area
        return [
            numeric(p + "-e", "easy", f"A rectangle has length {length} and width {width}. What is its area?", area,
                    f"長方形面積 = 長×寬 = {length}×{width}={area}。", "Geometry"),
            numeric(p + "-m", "medium", f"A rectangle has length {length} and width {width}. If both dimensions are doubled, what is its new perimeter?", 2*(2*length+2*width),
                    f"新長寬為 {2*length}、{2*width}，周長為 2({2*length}+{2*width})={4*(length+width)}。", "Geometry"),
            comparison(p + "-h", "hard", f"A rectangle has length {length} and width {width}.", "Its numerical area", "Its numerical perimeter", 0 if area>perimeter else 1 if area<perimeter else 2,
                       f"面積數值為 {area}；周長數值為 {perimeter}。", "Geometry"),
            numeric(p + "-x", "hard", f"A {length}-by-{width} rectangle is surrounded by a uniform border {border} units wide. What is the area of the border?", border_area,
                    f"外框面積 = ({length}+2×{border})({width}+2×{border})−{area}={border_area}。", "Geometry"),
        ]
    if family == 1:
        triple = [(3,4,5),(5,12,13),(8,15,17)][variant%3]
        scale = 1 + variant//3
        a,b,c = [x*scale for x in triple]
        area = Fraction(a*b,2)
        altitude = Fraction(a*b,c)
        return [
            numeric(p + "-e", "easy", f"A right triangle has legs {a} and {b}. What is the length of its hypotenuse?", c,
                    f"畢氏定理：√({a}²+{b}²)={c}。", "Geometry"),
            numeric(p + "-m", "medium", f"What is the area of a right triangle with legs {a} and {b}?", area,
                    f"面積 = 1/2×{a}×{b}={clean_number(area)}。", "Geometry"),
            numeric(p + "-h", "hard", f"In that triangle, what is the altitude from the right angle to the hypotenuse?", altitude,
                    f"以兩種底高表示同一面積：ab/2=ch/2，所以 h=ab/c={clean_number(altitude)}。", "Geometry",
                    stimulus=f"The right triangle has legs {a} and {b} and hypotenuse {c}."),
            comparison(p + "-x", "medium", f"A right triangle has legs {a} and {b}.", "The longer leg", "The hypotenuse", 1,
                       "直角三角形的斜邊一定比任一股長。", "Geometry"),
        ]
    if family == 2:
        radius = 3 + variant
        inner = radius - 1 - variant % 2
        sector_angle = [30,45,60,90,120][variant%5]
        ring_coefficient = radius * radius - inner * inner
        sector_coefficient = Fraction(radius * radius * sector_angle, 360)
        return [
            single(p + "-e", "easy", f"A circle has radius {radius}. What is its circumference?",
                   [f"{k}π" for k in {radius,2*radius,radius*radius,2*radius*radius,4*radius}], f"{2*radius}π",
                   f"圓周長 C=2πr=2π×{radius}={2*radius}π。", "Geometry"),
            single(p + "-m", "medium", f"A circle has radius {radius}. What is its area?",
                   [f"{k}π" for k in {radius,2*radius,radius*radius,2*radius*radius,4*radius}], f"{radius*radius}π",
                   f"圓面積 A=πr²={radius*radius}π。", "Geometry"),
            single(p + "-h", "hard", f"A circular ring has outer radius {radius} and inner radius {inner}. What is its area?",
                   [f"{k}π" for k in (ring_coefficient, ring_coefficient + 1, radius * radius,
                                                    inner * inner, radius * radius + inner * inner)], f"{ring_coefficient}π",
                   f"圓環面積 = π({radius}²−{inner}²)={ring_coefficient}π。", "Geometry"),
            single(p + "-x", "hard", f"A {sector_angle}° sector is cut from a circle of radius {radius}. What is the sector's area?",
                   [f"{clean_number(k)}π" for k in (sector_coefficient, sector_coefficient + 1,
                                                              sector_coefficient * 2, Fraction(radius * radius, 2),
                                                              radius * radius - sector_coefficient)], f"{clean_number(sector_coefficient)}π",
                   f"扇形占整圓 {sector_angle}/360，面積為 {sector_angle}/360×{radius}²π={clean_number(sector_coefficient)}π。", "Geometry"),
        ]
    if family == 3:
        length, width, height = 4+variant, 3+variant%5, 2+(variant//3)%6
        volume = length*width*height
        surface = 2*(length*width+length*height+width*height)
        cube_side = math.gcd(math.gcd(length,width),height)
        cubes = volume//(cube_side**3)
        return [
            numeric(p + "-e", "easy", f"A rectangular solid measures {length} by {width} by {height}. What is its volume?", volume,
                    f"體積 = {length}×{width}×{height}={volume}。", "Geometry"),
            numeric(p + "-m", "medium", "What is the surface area of that rectangular solid?", surface,
                    f"表面積 = 2(lw+lh+wh)=2({length*width}+{length*height}+{width*height})={surface}。", "Geometry", stimulus=f"Dimensions: {length} by {width} by {height}."),
            single(p + "-h", "hard", f"The solid is filled by the largest congruent cubes whose side length is an integer. How many cubes are needed?",
                   [str(max(1,cubes+d)) for d in (-length,-2,0,2,length)], str(cubes),
                   f"最大立方體邊長為三邊長的最大公因數 {cube_side}；個數為 {volume}/{cube_side**3}={cubes}。", "Geometry", stimulus=f"Dimensions: {length} by {width} by {height}."),
            comparison(p + "-x", "hard", f"A rectangular solid has dimensions {length}, {width}, and {height}.", "Its volume", "Its surface area", 0 if volume>surface else 1 if volume<surface else 2,
                       f"體積為 {volume}；表面積為 {surface}。", "Geometry"),
        ]
    if family == 4:
        small = 2 + variant
        large = small + 3 + variant % 4
        side = 6 + variant
        matching = Fraction(side*large,small)
        area_ratio = Fraction(small*small,large*large)
        volume_ratio = Fraction(small**3,large**3)
        return [
            numeric(p + "-e", "easy", f"Two similar figures have corresponding side-length ratio {small}:{large}. If the smaller corresponding side is {side}, what is the larger side?", matching,
                    f"對應邊成比例：較大邊 = {side}×{large}/{small}={clean_number(matching)}。", "Geometry"),
            single(p + "-m", "medium", "What is the ratio of the areas of the smaller figure to the larger figure?",
                   [f"{small}:{large}",f"{small*small}:{large*large}",f"{small**3}:{large**3}",f"{large}:{small}",f"{large*large}:{small*small}"], f"{small*small}:{large*large}",
                   "相似圖形面積比是對應邊比的平方。", "Geometry", stimulus=f"Corresponding side-length ratio (smaller to larger): {small}:{large}."),
            comparison(p + "-h", "hard", f"Two similar solids have corresponding length ratio {small}:{large}.", "Their volume ratio (smaller to larger)", f"{small*small}:{large*large}", 1 if volume_ratio < area_ratio else 0 if volume_ratio > area_ratio else 2,
                       f"體積比為 {small**3}:{large**3}，和邊長比的三次方一致；再與平方比比較。", "Geometry"),
            single(p + "-x", "hard", "What is the ratio of the volumes of the smaller solid to the larger solid?",
                   [f"{small}:{large}",f"{small*small}:{large*large}",f"{small**3}:{large**3}",f"{large**3}:{small**3}",f"{large*large}:{small*small}"], f"{small**3}:{large**3}",
                   "相似立體體積比是對應邊比的三次方。", "Geometry", stimulus=f"Corresponding side-length ratio (smaller to larger): {small}:{large}."),
        ]
    sides = 3 + variant
    interior_sum = (sides-2)*180
    each_interior = Fraction(interior_sum,sides)
    diagonals = sides*(sides-3)//2
    exterior = Fraction(360,sides)
    return [
        numeric(p + "-e", "easy", f"What is the sum of the interior angles of a {sides}-sided polygon?", interior_sum,
                f"內角和 = (n−2)×180°=({sides}−2)×180°={interior_sum}°。", "Geometry"),
        numeric(p + "-m", "medium", f"A regular polygon has {sides} sides. What is the measure of each interior angle, in degrees?", each_interior,
                f"正多邊形每一內角 = {interior_sum}/{sides}={clean_number(each_interior)}°。", "Geometry"),
        numeric(p + "-h", "hard", f"How many diagonals does a {sides}-sided polygon have?", diagonals,
                f"每頂點可連 {sides-3} 條對角線，但每條被算兩次，所以共有 {sides}({sides-3})/2={diagonals}。", "Geometry"),
        numeric(p + "-x", "medium", f"What is the measure of each exterior angle of a regular {sides}-sided polygon, in degrees?", exterior,
                f"正多邊形外角和為 360°，每一外角為 360/{sides}={clean_number(exterior)}°。", "Geometry"),
    ]


def data_analysis(family: int, variant: int) -> list[dict]:
    p = f"bq-d{family + 1}-{variant + 1:02d}"
    labels = ["North", "South", "East", "West"]
    if family == 0:
        first = [30+variant, 42+2*variant, 36+variant%7, 48+variant]
        second = [value + delta for value,delta in zip(first,[5,-3,8,2])]
        fig = chart("table", f"Regional applications — dataset {variant+1}", headers=["Region","Year 1","Year 2"],
                    rows=[[label,str(a),str(b)] for label,a,b in zip(labels,first,second)])
        total2=sum(second); increase=sum(second)-sum(first)
        statements=[second[0]>first[0], second[1]>first[1], second[2]-first[2]>second[3]-first[3], sum(second)>sum(first), max(second)==second[2]]
        return [
            numeric(p+"-e","easy","How many applications were received in Year 2 across all four regions?",total2,
                    f"將 Year 2 四列相加：{' + '.join(map(str,second))}={total2}。","Data Analysis",figure=fig),
            numeric(p+"-m","medium","By how many did the total number of applications change from Year 1 to Year 2?",increase,
                    f"Year 2 總數 {total2} 減 Year 1 總數 {sum(first)}，得到 {increase}。","Data Analysis",figure=fig),
            multiple(p+"-h","hard","Select each statement supported by the table.",[
                "North increased.","South increased.","East increased more than West.","The overall total increased.","East had the greatest Year 2 value."],
                {i for i,x in enumerate(statements) if x},"逐列比較並計算總和；正確敘述為所有計算結果為真的選項。","Data Analysis",figure=fig),
            numeric(p+"-x","medium","What was the arithmetic mean of the four Year 2 values?",Fraction(total2,4),
                    f"平均數 = {total2}/4={clean_number(Fraction(total2,4))}。","Data Analysis",figure=fig),
        ]
    if family == 1:
        categories=["A","B","C","D"]
        old=[40+variant,55+2*variant,35+variant%9,60+variant]
        new=[x+d for x,d in zip(old,[10,5,15,-5])]
        fig=chart("groupedBar",f"Program enrollment — dataset {variant+1}",x_axis="Program",y_axis="Students",
                  data=[series("Spring",[point(label=l,value=x) for l,x in zip(categories,old)]),series("Fall",[point(label=l,value=x) for l,x in zip(categories,new)])])
        diffs=[b-a for a,b in zip(old,new)]
        greatest=categories[diffs.index(max(diffs))]
        pct=Fraction((new[0]-old[0])*100,old[0])
        return [
            single(p+"-e","easy","Which program had the greatest increase from Spring to Fall?",categories,greatest,
                   f"四組變化為 {', '.join(map(str,diffs))}；最大增加量屬於 Program {greatest}。","Data Analysis",figure=fig),
            numeric(p+"-m","medium","What was the percent increase for Program A?",pct,
                    f"增加 {new[0]-old[0]}，以原值 {old[0]} 為分母：({new[0]-old[0]})/{old[0]}×100={clean_number(pct)}%。","Data Analysis",figure=fig),
            comparison(p+"-h","hard","Use the grouped bar chart.","The total Fall enrollment","The total Spring enrollment",0 if sum(new)>sum(old) else 1 if sum(new)<sum(old) else 2,
                       f"Fall 總數 {sum(new)}；Spring 總數 {sum(old)}。","Data Analysis",figure=fig),
            numeric(p+"-x","medium","How many students were enrolled across all programs and both terms?",sum(old)+sum(new),
                    f"兩組資料總和為 {sum(old)}+{sum(new)}={sum(old)+sum(new)}。","Data Analysis",figure=fig),
        ]
    if family == 2:
        values=[20+variant,24+variant,31+variant,29+variant,38+variant]
        fig=chart("line",f"Weekly output — dataset {variant+1}",x_axis="Week",y_axis="Units",
                  data=[series("Output",[point(x=i+1,value=x) for i,x in enumerate(values)])])
        changes=[values[i+1]-values[i] for i in range(4)]
        mean=Fraction(sum(values),5)
        return [
            numeric(p+"-e","easy","How many units did output change from Week 1 to Week 5?",values[-1]-values[0],
                    f"末值減初值：{values[-1]}−{values[0]}={values[-1]-values[0]}。","Data Analysis",figure=fig),
            numeric(p+"-m","medium","What was the mean weekly output over the five weeks?",mean,
                    f"五週總和 {sum(values)} 除以 5，得 {clean_number(mean)}。","Data Analysis",figure=fig),
            multiple(p+"-h","hard","Select each interval during which output increased.",[f"Week {i} to Week {i+1}" for i in range(1,5)],{i for i,x in enumerate(changes) if x>0},
                     f"相鄰變化依序為 {', '.join(map(str,changes))}；正值的區間才是增加。","Data Analysis",figure=fig),
            comparison(p+"-x","hard","Use the line chart.","The median weekly output","The mean weekly output",0 if sorted(values)[2]>mean else 1 if sorted(values)[2]<mean else 2,
                       f"中位數為 {sorted(values)[2]}，平均數為 {clean_number(mean)}。","Data Analysis",figure=fig),
        ]
    if family == 3:
        counts=[4+variant%4,8+variant%5,12+variant%6,7+variant%3,3+variant%4]
        bins=["0–9","10–19","20–29","30–39","40–49"]
        fig=chart("histogram",f"Score distribution — dataset {variant+1}",x_axis="Score interval",y_axis="Frequency",
                  data=[series("Students",[point(label=l,value=x) for l,x in zip(bins,counts)])])
        total=sum(counts); cumulative=0; median_bin=bins[0]
        for label,count in zip(bins,counts):
            cumulative+=count
            if cumulative>=Fraction(total,2): median_bin=label; break
        at_least30=counts[3]+counts[4]
        return [
            numeric(p+"-e","easy","How many observations are represented by the histogram?",total,
                    f"頻數總和為 {'+'.join(map(str,counts))}={total}。","Data Analysis",figure=fig),
            single(p+"-m","medium","Which interval contains the median observation?",bins,median_bin,
                   f"共有 {total} 筆；累積頻數首次涵蓋中央位置的組距是 {median_bin}。","Data Analysis",figure=fig),
            comparison(p+"-h","hard","Use the histogram; exact values within each interval are not given.","The mean score","25",3,
                       "直方圖只給組距與頻數，組內精確值未知，因此平均數與 25 的關係可能改變。","Data Analysis",figure=fig),
            numeric(p+"-x","medium","How many observations are at least 30?",at_least30,
                    f"至少 30 包含 30–39 與 40–49，合計 {counts[3]}+{counts[4]}={at_least30}。","Data Analysis",figure=fig),
        ]
    if family == 4:
        low=5+variant; q1=low+4; median=q1+5; q3=median+6; high=q3+5+variant%4
        low2=low-2; q12=q1+1; median2=median+variant%3; q32=q3+3; high2=high+4
        fig=chart("boxPlot",f"Two sample summaries — dataset {variant+1}",x_axis="Value",
                  data=[series("Sample A",[point(low=low,q1=q1,median=median,q3=q3,high=high)]),series("Sample B",[point(low=low2,q1=q12,median=median2,q3=q32,high=high2)])])
        iqra=q3-q1; iqrb=q32-q12; rangea=high-low; rangeb=high2-low2
        statements=[median2>=median,iqrb>iqra,rangeb>rangea,q1<median<q3,low2==q12]
        return [
            numeric(p+"-e","easy","What is the interquartile range of Sample A?",iqra,
                    f"IQR=Q3−Q1={q3}−{q1}={iqra}。","Data Analysis",figure=fig),
            comparison(p+"-m","medium","Use the box plots.","The range of Sample A","The range of Sample B",0 if rangea>rangeb else 1 if rangea<rangeb else 2,
                       f"A 的全距 {high}−{low}={rangea}；B 的全距 {high2}−{low2}={rangeb}。","Data Analysis",figure=fig),
            multiple(p+"-h","hard","Select each statement that must be true from the box plots.",[
                "Sample B's median is at least Sample A's median.","Sample B has the larger IQR.","Sample B has the larger range.","Sample A's median lies between its quartiles.","Sample B's minimum equals its first quartile."],
                {i for i,x in enumerate(statements) if x},"由五數摘要逐一比較中位數、四分位距與全距即可；盒形圖不提供平均數。","Data Analysis",figure=fig),
            numeric(p+"-x","medium","What is the range of Sample B?",rangeb,
                    f"全距 = 最大值−最小值 = {high2}−{low2}={rangeb}。","Data Analysis",figure=fig),
        ]
    if variant % 2 == 0:
        mean=60+variant; sd=5+variant%4
        fig=chart("normalCurve",f"Normally distributed measurements — dataset {variant+1}",x_axis="Measurement",y_axis="Relative frequency",
                  data=[series("Distribution",[point(x=mean+k*sd,value=math.exp(-0.5*k*k)) for k in (-3,-2,-1,0,1,2,3)])])
        return [
            single(p+"-e","easy","Approximately what percent of observations are within one standard deviation of the mean?",["34%","50%","68%","95%","99.7%"],"68%",
                   "常態分配的 68–95–99.7 規則指出，平均數正負一個標準差內約有 68%。","Data Analysis",figure=fig),
            numeric(p+"-m","medium","What measurement is two standard deviations above the mean?",mean+2*sd,
                    f"{mean}+2×{sd}={mean+2*sd}。","Data Analysis",figure=fig),
            multiple(p+"-h","hard","Select each statement that is approximately true for this normal distribution.",[
                "Half the observations are above the mean.","About 95% are within two standard deviations of the mean.","The mean and median are equal.","About 68% are above one standard deviation below the mean.","The distribution is symmetric."],{0,1,2,4},
                "常態分配對稱，平均數等於中位數；約 95% 落在 ±2σ。高於 μ−σ 的比例約 84%，不是 68%。","Data Analysis",figure=fig),
            comparison(p+"-x","hard","The distribution is normal.","The percentage above the mean","The percentage below the mean",2,
                       "常態分配以平均數為對稱中心，兩側各占 50%。","Data Analysis",figure=fig),
        ]
    total=100+2*variant; both=10+variant; only_a=25+variant%8; only_b=30+variant%7
    neither=total-only_a-only_b-both; a=only_a+both; b=only_b+both
    annotations=[
        {"label":"A only","value":str(only_a),"x":0.34,"y":0.50},
        {"label":"A and B","value":str(both),"x":0.50,"y":0.50},
        {"label":"B only","value":str(only_b),"x":0.66,"y":0.50},
        {"label":"Neither","value":"","x":0.84,"y":0.82},
    ]
    fig=chart("venn",f"Membership in groups A and B — dataset {variant+1}",caption=f"Total: {total}",annotations=annotations)
    return [
        numeric(p+"-e","easy","How many members are in group A?",a,
                f"A 包含 A only 與交集：{only_a}+{both}={a}。","Data Analysis",figure=fig),
        single(p+"-m","medium","If one member of group B is selected at random, what is the probability that the member is also in group A?",
               [clean_number(Fraction(both+d,b)) for d in (-5,-2,0,2,5)],clean_number(Fraction(both,b)),
               f"條件母體是 B，共 {b} 人；其中交集 {both} 人，所以機率 {both}/{b}={clean_number(Fraction(both,b))}。","Data Analysis",figure=fig),
        multiple(p+"-h","hard","Select each expression that equals the number in exactly one of the two groups.",[
            f"{a} + {b} − 2({both})",f"{only_a} + {only_b}",f"{a} + {b} − {both}",f"{total} − {neither} − {both}",f"{total} − {neither}"],{0,1,3},
            f"恰屬一組為 {only_a}+{only_b}={only_a+only_b}；前三個正確式中的第 1、2 項及扣除 neither 與交集的式子皆相等。","Data Analysis",figure=fig),
        numeric(p+"-x","medium","How many members are in neither group?",neither,
                f"總數扣除三個互斥區域：{total}−{only_a}−{both}−{only_b}={neither}。","Data Analysis",figure=fig),
    ]


BUILDERS = [
    *(lambda variant, family=family: arithmetic(family, variant) for family in range(6)),
    *(lambda variant, family=family: algebra(family, variant) for family in range(6)),
    *(lambda variant, family=family: geometry(family, variant) for family in range(6)),
    *(lambda variant, family=family: data_analysis(family, variant) for family in range(6)),
]


def generate() -> list[dict]:
    questions: list[dict] = []
    scenario_instances: list[tuple[int, int, list[dict]]] = []
    for builder_index, builder in enumerate(BUILDERS):
        for variant in range(VARIANTS_PER_FAMILY):
            generated = builder(variant)
            questions.extend(generated[:3])
            scenario_instances.append((builder_index, variant, generated))

    # Add fourth prompts in round-robin area order until the exact balance
    # target is reached. Cycling difficulty keeps the three tiers within one.
    scenarios_per_area = 6 * VARIANTS_PER_FAMILY
    by_area = [
        scenario_instances[i * scenarios_per_area:(i + 1) * scenarios_per_area]
        for i in range(4)
    ]
    ordered: list[tuple[int, int, list[dict]]] = []
    for position in range(scenarios_per_area):
        for area in range(4):
            ordered.append(by_area[area][position])
    extra_count = TARGET_TOTAL - len(questions)
    if not 0 <= extra_count <= len(ordered):
        raise ValueError(f"Target {TARGET_TOTAL} cannot be met by the available scenarios")
    for extra_index, (_, _, generated) in enumerate(ordered[:extra_count]):
        extra = generated[3]
        extra["difficulty"] = ("easy", "medium", "hard")[extra_index % 3]
        questions.append(extra)
    return questions


def normalized_signature(question: dict) -> str:
    text = " | ".join(str(question.get(key) or "") for key in
                      ("format", "stimulus", "prompt", "quantityA", "quantityB"))
    text = text.lower()
    text = re.sub(r"\$?\d+(?:\.\d+)?%?", "#", text)
    return re.sub(r"\s+", " ", text).strip()


def exact_content_signature(question: dict) -> str:
    figure = question.get("figure") or {}
    text = "|".join(str(value) for value in (
        figure.get("title"), question.get("stimulus"), question.get("prompt"),
        question.get("quantityA"), question.get("quantityB"),
    ) if value)
    return "".join(
        character for character in text.lower()
        if not character.isspace() and not unicodedata.category(character).startswith("P")
    )


def validate(questions: list[dict]) -> dict:
    if len(questions) != TARGET_TOTAL:
        raise ValueError(f"Expected {TARGET_TOTAL:,} questions; generated {len(questions):,}")
    ids = [question["id"] for question in questions]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate generated IDs")
    for question in questions:
        if not question["prompt"].strip() or not question["explanation"].strip():
            raise ValueError(f"{question['id']}: empty prompt or explanation")
        if question["format"] == "numericEntry":
            if not question["acceptedNumericAnswers"]:
                raise ValueError(f"{question['id']}: missing numeric key")
        else:
            option_ids = {option["id"] for group in question["groups"] for option in group["options"]}
            answer_ids = {answer for answers in question["correctSelections"].values() for answer in answers}
            if not answer_ids or not answer_ids <= option_ids:
                raise ValueError(f"{question['id']}: invalid answer key")
            texts = [option["text"] for group in question["groups"] for option in group["options"]]
            if len(texts) != len(set(texts)):
                raise ValueError(f"{question['id']}: duplicate choices")
    signatures = Counter(normalized_signature(question) for question in questions)
    exact_signatures = Counter(exact_content_signature(question) for question in questions)
    duplicate_exact = [signature for signature, count in exact_signatures.items() if count > 1]
    if duplicate_exact:
        raise ValueError(f"Generated questions contain {len(duplicate_exact)} duplicate content groups")
    return {
        "generatedQuestions": len(questions),
        "byContentArea": dict(sorted(Counter(q["contentArea"] for q in questions).items())),
        "byDifficulty": dict(sorted(Counter(q["difficulty"] for q in questions).items())),
        "byFormat": dict(sorted(Counter(q["format"] for q in questions).items())),
        "normalizedStemFamilies": len(signatures),
        "largestNormalizedStemFamily": max(signatures.values()),
        "sourcePolicy": SOURCE["detail"],
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path,
                        default=Path(__file__).resolve().parents[1] / "Gre Simulation" / "Resources")
    args = parser.parse_args()
    questions = generate()
    manifest = validate(questions)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / "BalancedQuantQuestions.json").write_text(
        json.dumps(questions, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (args.output_dir / "BalancedQuantManifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
