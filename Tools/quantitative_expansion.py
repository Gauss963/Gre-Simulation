"""Original GRE-aligned data-analysis questions and structured figures.

The numerical scenarios and wording in this file are original. Topic coverage and
display types follow the public ETS Quantitative Reasoning outline and Math Review.
"""

from __future__ import annotations

import math
from collections import Counter


SOURCE = {
    "title": "Original ETS-aligned Data Analysis item",
    "detail": (
        "Original item modeled on the ETS Quantitative Reasoning content outline "
        "and GRE Math Review; no official question text copied"
    ),
    "isAuthorizedSourceItem": False,
}


def point(*, label=None, x=None, value=None, low=None, q1=None, median=None, q3=None, high=None):
    return {
        "label": label,
        "x": x,
        "value": value,
        "low": low,
        "q1": q1,
        "median": median,
        "q3": q3,
        "high": high,
    }


def series(name, points):
    return {"name": name, "points": points}


def figure(kind, title, *, caption=None, x_axis=None, y_axis=None, headers=None, rows=None, data=None, annotations=None):
    return {
        "kind": kind,
        "title": title,
        "caption": caption,
        "xAxisTitle": x_axis,
        "yAxisTitle": y_axis,
        "headers": headers,
        "rows": rows,
        "series": data or [],
        "annotations": annotations,
    }


def option_group(choices, maximum=1):
    return {
        "id": "main",
        "title": None,
        "options": [{"id": f"o{i}", "text": text} for i, text in enumerate(choices)],
        "maximumSelections": maximum,
    }


def base(qid, difficulty, fmt, prompt, explanation, figure_data=None, stimulus=None):
    return {
        "id": qid,
        "measure": "quantitative",
        "difficulty": difficulty,
        "format": fmt,
        "stimulus": stimulus,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [],
        "correctSelections": {},
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": "Data Analysis",
        "source": SOURCE,
        "figure": figure_data,
    }


def single(qid, difficulty, prompt, choices, correct, explanation, figure_data=None, stimulus=None):
    item = base(qid, difficulty, "singleChoice", prompt, explanation, figure_data, stimulus)
    item["groups"] = [option_group(choices)]
    item["correctSelections"] = {"main": [f"o{correct}"]}
    return item


def multiple(qid, difficulty, prompt, choices, correct, explanation, figure_data=None, stimulus=None):
    item = base(qid, difficulty, "multipleChoice", prompt, explanation, figure_data, stimulus)
    item["groups"] = [option_group(choices, len(choices))]
    item["correctSelections"] = {"main": [f"o{i}" for i in sorted(correct)]}
    return item


def numeric(qid, difficulty, prompt, answers, explanation, figure_data=None, stimulus=None):
    item = base(qid, difficulty, "numericEntry", prompt, explanation, figure_data, stimulus)
    item["acceptedNumericAnswers"] = [str(answer) for answer in answers]
    return item


def comparison(qid, difficulty, stimulus, quantity_a, quantity_b, correct, explanation, figure_data=None):
    choices = [
        "Quantity A is greater.",
        "Quantity B is greater.",
        "The two quantities are equal.",
        "The relationship cannot be determined from the information given.",
    ]
    item = single(qid, difficulty, "Compare Quantity A and Quantity B.", choices, correct, explanation, figure_data, stimulus)
    item["format"] = "quantitativeComparison"
    item["quantityA"] = quantity_a
    item["quantityB"] = quantity_b
    return item


def normal_points(mean, standard_deviation):
    values = []
    for step in range(-30, 31):
        x = mean + standard_deviation * step / 10
        z = (x - mean) / standard_deviation
        density = math.exp(-0.5 * z * z) / standard_deviation
        values.append(point(x=round(x, 2), value=round(density, 6)))
    return values


def figure_sets():
    q = []

    sales = figure(
        "table",
        "Quarterly beverage sales",
        caption="Number of beverages sold, in hundreds",
        headers=["Shop", "Q1", "Q2", "Q3", "Q4"],
        rows=[["A", "120", "135", "150", "165"], ["B", "100", "140", "160", "180"], ["C", "80", "100", "120", "140"]],
    )
    q += [
        numeric("da-table-01", "easy", "How many hundreds of beverages did Shop A sell during all four quarters?", [570], "Add Shop A's four entries: 120 + 135 + 150 + 165 = 570. Since the table is measured in hundreds, the displayed-unit answer is 570.", sales),
        single("da-table-02", "medium", "Shop B's Q4 sales were what percent greater than its Q1 sales?", ["40%", "60%", "80%", "100%", "180%"], 2, "Shop B increased from 100 to 180, a gain of 80. Percent increase uses the original 100 as the base: 80/100 × 100% = 80%.", sales),
        multiple("da-table-03", "hard", "Select each quarter in which the three shops together sold more than 400 hundreds of beverages.", ["Q1", "Q2", "Q3", "Q4"], {2, 3}, "The quarterly totals are 300, 375, 430, and 485. Only Q3 and Q4 exceed 400; Q2 is below the threshold even though it is close.", sales),
    ]

    visitors = figure(
        "bar",
        "Library visitors by weekday",
        y_axis="Visitors",
        data=[series("Visitors", [point(label=day, value=value) for day, value in zip(["Mon", "Tue", "Wed", "Thu", "Fri"], [180, 240, 210, 300, 270])])],
    )
    q += [
        numeric("da-bar-01", "easy", "What was the mean number of visitors per weekday?", [240], "The five bars total 180 + 240 + 210 + 300 + 270 = 1,200. Dividing by 5 weekdays gives a mean of 240 visitors.", visitors),
        single("da-bar-02", "medium", "The number of visitors on Thursday was what percent greater than on Tuesday?", ["20%", "25%", "30%", "40%", "60%"], 1, "Thursday exceeds Tuesday by 300 − 240 = 60 visitors. Relative to Tuesday, the percent increase is 60/240 = 0.25, or 25%.", visitors),
        multiple("da-bar-03", "hard", "Select each statement that is true.", ["Exactly two days had more visitors than the five-day mean.", "The median daily count was 240.", "Friday had 50% more visitors than Monday.", "The range was 100 visitors."], {0, 1, 2}, "The mean is 240, and only Thursday and Friday are above it. Ordering the values gives 180, 210, 240, 270, 300, so the median is 240. Friday's 90-person advantage over Monday is 90/180 = 50%. The range is 300 − 180 = 120, so the final statement is false.", visitors),
    ]

    commute = figure(
        "groupedBar",
        "Employees by primary commuting mode",
        caption="Two annual surveys",
        y_axis="Employees",
        data=[
            series("2024", [point(label=x, value=y) for x, y in zip(["Bus", "Rail", "Bicycle", "Car"], [120, 180, 60, 240])]),
            series("2025", [point(label=x, value=y) for x, y in zip(["Bus", "Rail", "Bicycle", "Car"], [150, 210, 90, 210])]),
        ],
    )
    q += [
        numeric("da-grouped-bar-01", "easy", "By how many employees did the total represented in the survey increase from 2024 to 2025?", [60], "The 2024 total is 120 + 180 + 60 + 240 = 600. The 2025 total is 150 + 210 + 90 + 210 = 660, so the increase is 60 employees.", commute),
        single("da-grouped-bar-02", "medium", "Which commuting mode had the greatest percent increase from 2024 to 2025?", ["Bus", "Rail", "Bicycle", "Car"], 2, "The percentage changes are 30/120 = 25% for bus, 30/180 ≈ 16.7% for rail, and 30/60 = 50% for bicycle; car decreased. Bicycle therefore had the greatest percent increase.", commute),
        multiple("da-grouped-bar-03", "hard", "Select each statement supported by the graph.", ["The total rose by 10%.", "Rail represented a larger share of the total in 2025 than in 2024.", "The number commuting by car fell by 12.5%.", "In 2025, bus and bicycle together equaled rail."], {0, 2}, "The total rose from 600 to 660, which is 60/600 = 10%. Rail's shares are 180/600 = 30% and 210/660 ≈ 31.8%, so that statement is actually true as well. Car fell by 30/240 = 12.5%. Bus plus bicycle in 2025 is 240, not 210.", commute),
    ]
    # Correct the grouped-bar multi-select key after laying out the complete calculation.
    q[-1]["correctSelections"] = {"main": ["o0", "o1", "o2"]}

    reservoir = figure(
        "line",
        "Reservoir level at the end of each month",
        x_axis="Month",
        y_axis="Level (meters)",
        data=[series("Level", [point(x=x, value=y) for x, y in enumerate([42, 46, 45, 51, 55, 54], 1)])],
    )
    q += [
        numeric("da-line-01", "easy", "What was the net increase in the reservoir level from month 1 to month 6, in meters?", [12], "Use the endpoints, not the sum of all monthly changes. The net increase is 54 − 42 = 12 meters.", reservoir),
        single("da-line-02", "medium", "Between which consecutive months was the increase greatest?", ["1 and 2", "2 and 3", "3 and 4", "4 and 5", "5 and 6"], 2, "The consecutive changes are +4, −1, +6, +4, and −1 meters. The largest increase, 6 meters, occurred between months 3 and 4.", reservoir),
        multiple("da-line-03", "hard", "Select each statement that must be true.", ["The median of the six levels was 48.5 meters.", "The mean level was greater than the median level.", "The range was 13 meters.", "The level increased during exactly three of the five intervals."], {0, 2, 3}, "Ordering the levels gives 42, 45, 46, 51, 54, 55, so the median is (46 + 51)/2 = 48.5 and the range is 55 − 42 = 13. The mean is 293/6 ≈ 48.83, which is greater than 48.5, so that statement is also true. Positive changes occur in intervals 1–2, 3–4, and 4–5.", reservoir),
    ]
    q[-1]["correctSelections"] = {"main": ["o0", "o1", "o2", "o3"]}

    orders = figure(
        "line",
        "Weekly orders by channel",
        x_axis="Week",
        y_axis="Orders",
        data=[
            series("Online", [point(x=x, value=y) for x, y in enumerate([80, 100, 120, 150, 180], 1)]),
            series("In store", [point(x=x, value=y) for x, y in enumerate([160, 150, 140, 130, 120], 1)]),
        ],
    )
    q += [
        single("da-dual-line-01", "easy", "In which week did online orders first exceed in-store orders?", ["Week 1", "Week 2", "Week 3", "Week 4", "Week 5"], 3, "Compare the two series week by week. Online orders are lower through week 3, then 150 exceeds 130 in week 4.", orders),
        numeric("da-dual-line-02", "medium", "How many orders, in total, were placed in week 4?", [280], "Week 4 has 150 online orders and 130 in-store orders. Adding the two channels gives 150 + 130 = 280.", orders),
        multiple("da-dual-line-03", "hard", "Select each statement that is true from week 1 through week 5.", ["Online orders increased by 125%.", "In-store orders decreased by 25%.", "The combined weekly total was constant.", "The total number of online orders exceeded the total number of in-store orders."], {0, 1}, "Online orders rose by 100 from a base of 80, so the increase was 100/80 = 125%. In-store orders fell by 40 from 160, or 25%. Combined totals are 240, 250, 260, 280, and 300, so they were not constant. The five-week online total is 630 versus 700 in store.", orders),
    ]

    budget = figure(
        "pie",
        "Monthly household budget",
        caption="Total budget: $2,400",
        data=[series("Budget", [point(label=x, value=y) for x, y in [("Housing 35%", 35), ("Food 20%", 20), ("Transport 15%", 15), ("Savings 20%", 20), ("Other 10%", 10)]])],
    )
    q += [
        numeric("da-pie-01", "easy", "How many dollars were budgeted for transportation?", [360], "Transportation is 15% of the $2,400 total. Thus 0.15 × 2,400 = $360.", budget),
        single("da-pie-02", "medium", "What is the ratio of the amount for housing to the amount for food?", ["7 to 3", "7 to 4", "5 to 3", "4 to 7", "3 to 2"], 1, "Both amounts are percentages of the same total, so compare 35:20 directly. Dividing both terms by 5 gives 7:4.", budget),
        multiple("da-pie-03", "hard", "Select each category for which more than $400 was budgeted.", ["Housing", "Food", "Transportation", "Savings", "Other"], {0, 1, 3}, "More than $400 out of $2,400 means more than 400/2,400 = 1/6, or about 16.7%. Housing is 35%, while food and savings are each 20%; the other categories do not exceed the cutoff.", budget),
    ]

    shipments = figure(
        "pie",
        "Shipments by region",
        caption="2024 total: 1,200 · 2025 total: 1,500",
        data=[
            series("2024", [point(label=x, value=y) for x, y in [("North 30%", 30), ("South 25%", 25), ("East 20%", 20), ("West 25%", 25)]]),
            series("2025", [point(label=x, value=y) for x, y in [("North 24%", 24), ("South 28%", 28), ("East 22%", 22), ("West 26%", 26)]]),
        ],
    )
    q += [
        numeric("da-dual-pie-01", "easy", "How many North-region shipments were made in 2025?", [360], "North received 24% of 1,500 shipments in 2025. The count is 0.24 × 1,500 = 360.", shipments),
        single("da-dual-pie-02", "medium", "Which region had the greatest increase in number of shipments from 2024 to 2025?", ["North", "South", "East", "West"], 1, "Convert percentages to counts. North stays at 360; South rises from 300 to 420 (+120); East rises from 240 to 330 (+90); West rises from 300 to 390 (+90). South has the largest increase.", shipments),
        multiple("da-dual-pie-03", "hard", "Select each statement that is true.", ["North had the same number of shipments in both years.", "East had 570 shipments across the two years.", "West's shipment count increased by 30%.", "South and West together made up more than half of 2025 shipments."], {0, 1, 2, 3}, "North is 30% of 1,200 = 360 and 24% of 1,500 = 360. East totals 240 + 330 = 570. West rises from 300 to 390, a 90/300 = 30% increase. In 2025, South plus West is 28% + 26% = 54%, which is more than half.", shipments),
    ]

    scores = figure(
        "histogram",
        "Distribution of 30 test scores",
        x_axis="Score interval",
        y_axis="Frequency",
        data=[series("Frequency", [point(label=x, value=y) for x, y in [("50–59", 2), ("60–69", 5), ("70–79", 9), ("80–89", 11), ("90–99", 3)]])],
    )
    q += [
        single("da-histogram-01", "easy", "Which interval contains the greatest number of scores?", ["50–59", "60–69", "70–79", "80–89", "90–99"], 3, "The tallest bar has frequency 11 and corresponds to the 80–89 interval. A histogram identifies an interval, not an exact most frequent score.", scores),
        numeric("da-histogram-02", "medium", "How many scores were at least 80?", [14], "Scores of at least 80 are in the 80–89 and 90–99 intervals. Their frequencies total 11 + 3 = 14.", scores),
        multiple("da-histogram-03", "hard", "Select each statement that must be true.", ["The median score lies in the 70–79 interval.", "The mean score is greater than 75.", "Exactly 7 scores were below 70.", "The range of the scores is at least 31."], {0, 2, 3}, "For 30 values, the median uses positions 15 and 16. Cumulative frequency reaches 7 below 70 and 16 by 79, so both middle positions are in 70–79. Exactly 2 + 5 = 7 scores are below 70. With at least one score in 50–59 and one in 90–99, the smallest possible range is 90 − 59 = 31. Interval data do not fix the mean, so it need not exceed 75.", scores),
    ]

    durations = figure(
        "histogram",
        "Relative frequency of commute times",
        caption="Survey of 200 commuters",
        x_axis="Minutes",
        y_axis="Relative frequency",
        data=[series("Relative frequency", [point(label=x, value=y) for x, y in [("0–9", .10), ("10–19", .25), ("20–29", .35), ("30–39", .20), ("40–49", .10)]])],
    )
    q += [
        numeric("da-relative-histogram-01", "easy", "How many surveyed commuters had commute times from 20 through 29 minutes?", [70], "The interval's relative frequency is 0.35. Multiplying by the 200 commuters gives 0.35 × 200 = 70.", durations),
        single("da-relative-histogram-02", "medium", "What percent of the commuters had commute times less than 30 minutes?", ["35%", "60%", "65%", "70%", "90%"], 3, "Less than 30 minutes includes the first three intervals. Their relative frequencies sum to 0.10 + 0.25 + 0.35 = 0.70, or 70%.", durations),
        multiple("da-relative-histogram-03", "hard", "Select each statement that must be true.", ["The median commute time lies in the 20–29 minute interval.", "Exactly 20 commuters had times from 40 through 49 minutes.", "The mean commute time is 24.5 minutes.", "At least 90 commuters had commute times of 30 minutes or more."], {0, 1}, "Cumulative relative frequency is 0.35 below 20 and 0.70 below 30, so the median lies in 20–29. The last interval contains 0.10 × 200 = 20 commuters. A histogram does not reveal exact values within bins, so the exact mean is unknown. Times of 30 minutes or more account for 0.30 × 200 = 60, not at least 90.", durations),
    ]

    scatter = figure(
        "scatter",
        "Advertising and weekly sales",
        x_axis="Advertising ($1,000s)",
        y_axis="Sales ($1,000s)",
        data=[
            series("Observed", [point(x=x, value=y) for x, y in [(1, 12), (2, 15), (3, 18), (4, 22), (5, 25), (6, 29)]]),
            series("Trend line", [point(x=1, value=11), point(x=6, value=29)]),
        ],
    )
    q += [
        single("da-scatter-01", "easy", "Which description best characterizes the association shown by the observed points?", ["Strong positive association", "Strong negative association", "No association", "A perfectly horizontal association", "A curved negative association"], 0, "As advertising increases, sales also increase, and the points remain close to an upward-sloping line. This is a strong positive association, though association alone does not prove causation.", scatter),
        numeric("da-scatter-02", "medium", "According to the trend line, predicted sales at an advertising level of $6,000 are how many thousands of dollars?", [29], "The trend line passes through the point with x = 6 and y = 29. Because both axes are in thousands, the numeric value requested is 29.", scatter),
        multiple("da-scatter-03", "hard", "Select each conclusion supported by the graph alone.", ["Observed sales generally rise as advertising rises.", "Advertising caused the change in sales.", "The observed point at x = 4 is above the trend line.", "All observed points lie exactly on the trend line."], {0, 2}, "The upward pattern supports a positive association. At x = 4, the line interpolates to about 21.8 while the observed value is 22, so the point is slightly above the line. A scatterplot alone cannot establish causation, and the points are not all exactly on the line.", scatter),
    ]

    classes = figure(
        "boxPlot",
        "Exam scores by class",
        caption="Each box plot shows minimum, Q1, median, Q3, and maximum",
        data=[
            series("Class A", [point(low=55, q1=68, median=76, q3=84, high=96)]),
            series("Class B", [point(low=50, q1=70, median=78, q3=88, high=98)]),
        ],
    )
    q += [
        numeric("da-box-01", "easy", "What is the interquartile range of the Class A scores?", [16], "For Class A, Q1 = 68 and Q3 = 84. The interquartile range is Q3 − Q1 = 84 − 68 = 16.", classes),
        single("da-box-02", "medium", "Which class has the greater range of scores?", ["Class A", "Class B", "The ranges are equal", "The relationship cannot be determined"], 1, "Class A's range is 96 − 55 = 41. Class B's range is 98 − 50 = 48, so Class B has the greater range.", classes),
        multiple("da-box-03", "hard", "Select each statement that is true.", ["Class B has the higher median.", "Class B has the greater interquartile range.", "At least 75% of Class A scores are at or above 68.", "Every Class B score is greater than the Class A median."], {0, 1, 2}, "The medians are 76 and 78, and the IQRs are 16 and 18, so the first two statements are true. Since Q1 for A is 68, at least 75% of its data are at or above 68. Class B's minimum is 50, below A's median, so the last statement is false.", classes),
    ]

    clinics = figure(
        "boxPlot",
        "Patient waiting times",
        caption="Minutes",
        data=[
            series("Clinic X", [point(low=5, q1=9, median=12, q3=15, high=20)]),
            series("Clinic Y", [point(low=7, q1=10, median=12, q3=14, high=18)]),
        ],
    )
    q += [
        single("da-dual-box-01", "easy", "How do the median waiting times compare?", ["Clinic X is greater", "Clinic Y is greater", "They are equal", "They cannot be compared"], 2, "The vertical median line is at 12 minutes for each clinic. Therefore the two medians are equal.", clinics),
        numeric("da-dual-box-02", "medium", "By how many minutes does Clinic X's interquartile range exceed Clinic Y's?", [2], "Clinic X has IQR 15 − 9 = 6. Clinic Y has IQR 14 − 10 = 4, so X exceeds Y by 2 minutes.", clinics),
        multiple("da-dual-box-03", "hard", "Select each statement that must be true.", ["Clinic X has the greater range.", "Clinic Y has the smaller interquartile range.", "The mean wait is 12 minutes at both clinics.", "At least half of Clinic Y waits are between 10 and 14 minutes, inclusive."], {0, 1, 3}, "The ranges are 20 − 5 = 15 for X and 18 − 7 = 11 for Y. Their IQRs are 6 and 4, so Y's is smaller. A box plot does not determine either mean. The interval from Q1 through Q3 contains the middle 50% of Clinic Y values, so at least half lie from 10 through 14 minutes.", clinics),
    ]

    curves = figure(
        "normalCurve",
        "Two normal distributions",
        caption="Distribution A: mean 50, standard deviation 5 · Distribution B: mean 50, standard deviation 10",
        x_axis="Value",
        data=[series("A (SD 5)", normal_points(50, 5)), series("B (SD 10)", normal_points(50, 10))],
    )
    q += [
        single("da-normal-01", "easy", "Which distribution has the greater mean?", ["Distribution A", "Distribution B", "The means are equal", "The relationship cannot be determined"], 2, "Both distributions are centered at 50, and the caption gives mean 50 for each. Different spreads do not change the equality of their means.", curves),
        numeric("da-normal-02", "medium", "For Distribution A, what value is 2 standard deviations above the mean?", [60], "Distribution A has mean 50 and standard deviation 5. Two standard deviations above the mean is 50 + 2(5) = 60.", curves),
        multiple("da-normal-03", "hard", "Using the 68–95–99.7 rule, select each statement that is approximately true.", ["About 68% of A lies from 45 to 55.", "About 95% of B lies from 30 to 70.", "A has more probability than B between 45 and 55.", "B has the higher peak because it is more spread out."], {0, 1, 2}, "The interval 45–55 is within 1 SD of A's mean, so it contains about 68%. The interval 30–70 is within 2 SDs of B's mean, so it contains about 95%. A is more concentrated around 50, so it places more probability in 45–55. Greater spread gives B a lower, not higher, peak.", curves),
    ]

    probability = figure(
        "table",
        "Probability distribution for X",
        headers=["x", "0", "1", "2", "3"],
        rows=[["P(X = x)", "0.10", "0.30", "0.40", "0.20"]],
    )
    q += [
        numeric("da-distribution-01", "easy", "What is P(X ≥ 2)?", ["0.6", "3/5"], "The outcomes satisfying X ≥ 2 are 2 and 3. Add their probabilities: 0.40 + 0.20 = 0.60 = 3/5.", probability),
        single("da-distribution-02", "medium", "What is the expected value of X?", ["1.0", "1.5", "1.7", "2.0", "2.4"], 2, "Multiply each value by its probability and add: 0(0.10) + 1(0.30) + 2(0.40) + 3(0.20) = 0 + 0.30 + 0.80 + 0.60 = 1.70.", probability),
        multiple("da-distribution-03", "hard", "Select each statement that is true.", ["The median of X is 2.", "The mode of X is 2.", "P(X < 2) = P(X > 2).", "The range of possible X values is 4."], {0, 1, 2}, "Cumulative probability first reaches 0.50 at X = 2, so 2 is a median. X = 2 also has the largest probability, so it is the mode. P(X < 2) = 0.10 + 0.30 = 0.40, while P(X > 2) = 0.20, so those are not equal. The range is 3 − 0 = 3.", probability),
    ]
    q[-1]["correctSelections"] = {"main": ["o0", "o1"]}

    venn = figure(
        "venn",
        "Membership in two school clubs",
        caption="There are 66 students in total",
        annotations=[
            {"label": "Coding only", "value": "24", "x": 0.30, "y": 0.53},
            {"label": "Both", "value": "14", "x": 0.50, "y": 0.53},
            {"label": "Chess only", "value": "18", "x": 0.70, "y": 0.53},
            {"label": "Neither", "value": "10", "x": 0.88, "y": 0.14},
            {"label": "Coding", "value": "", "x": 0.28, "y": 0.14},
            {"label": "Chess", "value": "", "x": 0.72, "y": 0.14},
        ],
    )
    q += [
        numeric("da-venn-01", "easy", "How many students are members of the coding club?", [38], "The coding circle contains 24 coding-only students and 14 students in both clubs. Thus the coding membership is 24 + 14 = 38.", venn),
        single("da-venn-02", "medium", "What fraction of all students are in exactly one of the two clubs?", ["7/11", "14/33", "19/33", "28/33", "31/33"], 0, "Exactly one club includes the two non-overlapping regions: 24 + 18 = 42 students. The fraction is 42/66, which reduces by 6 to 7/11.", venn),
        multiple("da-venn-03", "hard", "Select each statement that is true.", ["56 students are in at least one club.", "32 students are in the chess club.", "The probability a random student is in neither club is 5/33.", "More coding members than chess members belong to both clubs."], {0, 1, 2}, "The union contains 24 + 14 + 18 = 56 students. Chess membership is 14 + 18 = 32. The probability of neither is 10/66 = 5/33. The same 14 students form the overlap for both clubs, so the final comparison is false.", venn),
    ]

    assert len(q) == 45
    return q


def standalone_questions():
    return [
        comparison("da-stat-01", "easy", "Each value in list B is 7 greater than the corresponding value in list A.", "The range of list A", "The range of list B", 2, "Adding 7 shifts both the maximum and minimum by 7. Their difference is unchanged, so the two ranges are equal."),
        numeric("da-stat-02", "easy", "A class of 8 students has mean score 74. If a ninth student with score 83 joins, what is the new mean?", [75], "The original total is 8 × 74 = 592. Adding 83 gives 675; dividing by 9 gives a new mean of 75."),
        single("da-stat-03", "easy", "In an ordered list of 20 distinct values, which positions are averaged to find the median?", ["9th and 10th", "10th and 11th", "11th and 12th", "Only the 10th", "Only the 11th"], 1, "An even-sized list has two middle positions. For 20 values those positions are 20/2 = 10 and 11, so the median is their average."),
        multiple("da-stat-04", "easy", "A fair six-sided die is rolled once. Select each event with probability 1/2.", ["The result is even.", "The result is greater than 3.", "The result is prime.", "The result is at most 2."], {0, 1, 2}, "Even outcomes are {2,4,6}, outcomes greater than 3 are {4,5,6}, and prime outcomes are {2,3,5}; each set has 3 of 6 outcomes. Only 2 of 6 outcomes are at most 2."),
        numeric("da-stat-05", "easy", "A survey result is at the 82nd percentile. What percent of results are at or below it?", [82], "By definition, the 82nd percentile has approximately 82% of observations at or below it. The requested percent is therefore 82."),

        comparison("da-stat-06", "medium", "List A has standard deviation 4. List B is formed by multiplying every value in A by −3.", "The standard deviation of list B", "12", 2, "Multiplying all values by a constant multiplies standard deviation by the constant's absolute value. Thus SD(B) = |−3| × 4 = 12, equal to Quantity B."),
        numeric("da-stat-07", "medium", "A course grade is based on tests worth 70% and a project worth 30%. A student averages 84 on tests and earns 94 on the project. What is the weighted course score?", [87], "Weight each component before adding: 0.70(84) + 0.30(94) = 58.8 + 28.2 = 87."),
        single("da-stat-08", "medium", "A bag contains 5 red and 7 blue tiles. Two tiles are drawn without replacement. What is the probability both are red?", ["5/144", "5/33", "5/22", "25/144", "10/33"], 1, "The first red probability is 5/12. After a red is removed, it is 4/11. Multiplying gives (5/12)(4/11) = 20/132 = 5/33."),
        multiple("da-stat-09", "medium", "Events A and B are independent, with P(A) = 0.4 and P(B) = 0.5. Select each statement that must be true.", ["P(A and B) = 0.20", "P(A given B) = 0.40", "P(A or B) = 0.70", "A and B cannot occur together"], {0, 1, 2}, "Independence gives P(A∩B) = 0.4(0.5) = 0.20 and P(A|B) = P(A) = 0.40. Inclusion-exclusion gives P(A∪B) = 0.4 + 0.5 − 0.2 = 0.70. Independent events can occur together."),
        single("da-stat-10", "medium", "A game pays $12 with probability 1/4 and loses $4 with probability 3/4. What is the expected net payoff?", ["−$3", "$0", "$2", "$3", "$8"], 1, "The expected payoff is (1/4)($12) + (3/4)(−$4) = $3 − $3 = $0. Expected value is a long-run average, not a guaranteed result on one play."),

        comparison("da-stat-11", "hard", "List A consists of n values with mean 18. List B consists of 2n values with mean 12, where n is positive.", "The mean of the combined list", "14", 2, "The combined total is 18n + 12(2n) = 42n across 3n values. Its mean is 42n/(3n) = 14, equal to Quantity B."),
        numeric("da-stat-12", "hard", "Seven finalists are assigned gold, silver, and bronze medals, with no ties. In how many ways can the medals be assigned?", [210], "The medals are distinct, so order matters. There are 7 choices for gold, 6 remaining for silver, and 5 for bronze: 7 × 6 × 5 = 210."),
        single("da-stat-13", "hard", "A committee of 4 is chosen from 6 engineers and 5 designers. How many committees contain exactly 2 engineers and 2 designers?", ["60", "90", "120", "150", "225"], 3, "Choose the two groups independently: C(6,2) = 15 engineer pairs and C(5,2) = 10 designer pairs. Multiplying gives 15 × 10 = 150 committees."),
        multiple("da-stat-14", "hard", "A normally distributed variable has mean 100 and standard deviation 15. Using the 68–95–99.7 rule, select each statement that is approximately true.", ["68% of values lie from 85 to 115.", "95% lie from 70 to 130.", "2.5% lie above 130.", "99.7% lie from 55 to 145."], {0, 1, 2, 3}, "The intervals are respectively within 1, 2, and 3 standard deviations of the mean. About 68%, 95%, and 99.7% lie in those symmetric intervals. The 5% outside two standard deviations is split equally between two tails, leaving about 2.5% above 130."),
        comparison("da-stat-15", "hard", "A data set has Q1 = 20, median = 28, and Q3 = 36. One new value greater than every existing value is added.", "The interquartile range of the enlarged set", "16", 3, "The original IQR is 36 − 20 = 16, but adding one value can change the rank positions used for Q1 and Q3. Without the original number of observations and nearby values, the new IQR could equal or differ from 16, so the relationship cannot be determined."),
    ]


def data_analysis_questions():
    questions = figure_sets() + standalone_questions()
    assert len(questions) == 60
    assert Counter(item["difficulty"] for item in questions) == {"easy": 20, "medium": 20, "hard": 20}
    assert Counter(item["format"] for item in questions) == {
        "singleChoice": 19,
        "multipleChoice": 18,
        "numericEntry": 19,
        "quantitativeComparison": 4,
    }
    assert {item["figure"]["kind"] for item in questions if item["figure"]} == {
        "table", "bar", "groupedBar", "line", "pie", "scatter",
        "histogram", "boxPlot", "normalCurve", "venn",
    }
    assert all(len(item["explanation"]) >= 65 for item in questions)
    return questions
