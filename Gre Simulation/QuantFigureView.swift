import Charts
import SwiftUI

struct QuantFigureView: View {
    let figure: QuantFigure

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                Text(figure.title)
                    .font(.headline)
                    .foregroundStyle(.primary)
                if let caption = figure.caption {
                    Text(caption)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            figureBody

            if let xAxisTitle = figure.xAxisTitle {
                Text(xAxisTitle)
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(figure.title)
    }

    @ViewBuilder
    private var figureBody: some View {
        switch figure.kind {
        case .table:
            table
        case .bar:
            barChart(grouped: false)
        case .groupedBar:
            barChart(grouped: true)
        case .line:
            lineChart
        case .pie:
            pieCharts
        case .scatter:
            scatterChart
        case .histogram:
            histogram
        case .boxPlot:
            BoxPlotFigure(figure: figure)
        case .normalCurve:
            normalCurve
        case .venn:
            VennFigure(figure: figure)
        }
    }

    private var table: some View {
        ScrollView(.horizontal) {
            Grid(horizontalSpacing: 0, verticalSpacing: 0) {
                if let headers = figure.headers {
                    GridRow {
                        ForEach(Array(headers.enumerated()), id: \.offset) { _, header in
                            tableCell(header, isHeader: true)
                        }
                    }
                }
                ForEach(Array((figure.rows ?? []).enumerated()), id: \.offset) { rowIndex, row in
                    GridRow {
                        ForEach(Array(row.enumerated()), id: \.offset) { columnIndex, value in
                            tableCell(value, isHeader: columnIndex == 0, isAlternate: rowIndex.isMultiple(of: 2))
                        }
                    }
                }
            }
            .overlay(Rectangle().stroke(GRETheme.border))
        }
        .accessibilityLabel(accessibleTableText)
    }

    private func tableCell(_ text: String, isHeader: Bool, isAlternate: Bool = false) -> some View {
        Text(text)
            .font(.system(size: 13, weight: isHeader ? .semibold : .regular, design: .rounded))
            .monospacedDigit()
            .frame(minWidth: 82, maxWidth: .infinity, minHeight: 38)
            .padding(.horizontal, 8)
            .background(isHeader ? GRETheme.blue.opacity(0.13) : (isAlternate ? GRETheme.canvas.opacity(0.8) : GRETheme.surface))
            .overlay(alignment: .trailing) { Rectangle().fill(GRETheme.border).frame(width: 1) }
            .overlay(alignment: .bottom) { Rectangle().fill(GRETheme.border).frame(height: 1) }
    }

    private func barChart(grouped: Bool) -> some View {
        Chart(categoryData) { datum in
            BarMark(x: .value("Category", datum.label), y: .value("Value", datum.value))
                .foregroundStyle(by: .value("Series", datum.series))
                .position(by: .value("Series", grouped ? datum.series : "Value"))
        }
        .chartYAxisLabel(figure.yAxisTitle ?? "")
        .chartLegend(figure.series.count > 1 ? .visible : .hidden)
        .standardPlotStyle()
        .frame(minHeight: 270)
    }

    private var lineChart: some View {
        Chart(coordinateData) { datum in
            LineMark(x: .value("x", datum.x), y: .value("Value", datum.value), series: .value("Series", datum.series))
                .foregroundStyle(by: .value("Series", datum.series))
                .lineStyle(StrokeStyle(lineWidth: 2.5))
            PointMark(x: .value("x", datum.x), y: .value("Value", datum.value))
                .foregroundStyle(by: .value("Series", datum.series))
                .symbolSize(45)
        }
        .chartYAxisLabel(figure.yAxisTitle ?? "")
        .chartLegend(figure.series.count > 1 ? .visible : .hidden)
        .standardPlotStyle()
        .frame(minHeight: 270)
    }

    private var pieCharts: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 20) { pieChartItems }
            VStack(spacing: 22) { pieChartItems }
        }
    }

    private var pieChartItems: some View {
        ForEach(figure.series) { series in
            let slices = categoryData(for: series)
            VStack(spacing: 7) {
                if figure.series.count > 1 {
                    Text(series.name).font(.caption.bold())
                }
                Chart(slices) { datum in
                    SectorMark(angle: .value("Value", datum.value), innerRadius: .ratio(0.42), angularInset: 1)
                        .foregroundStyle(pieColor(datum.colorIndex))
                }
                .chartLegend(.hidden)
                .frame(minWidth: 185, minHeight: 210)
                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], alignment: .leading, spacing: 5) {
                    ForEach(slices) { datum in
                        HStack(spacing: 4) {
                            Circle().fill(pieColor(datum.colorIndex)).frame(width: 7, height: 7)
                            Text(datum.label).font(.caption2).lineLimit(1)
                        }
                    }
                }
            }
            .frame(maxWidth: .infinity)
        }
    }

    private var scatterChart: some View {
        Chart(coordinateData) { datum in
            if datum.series.localizedCaseInsensitiveContains("trend") {
                LineMark(x: .value("x", datum.x), y: .value("Value", datum.value))
                    .foregroundStyle(by: .value("Series", datum.series))
                    .lineStyle(StrokeStyle(lineWidth: 2, dash: [6, 4]))
            } else {
                PointMark(x: .value("x", datum.x), y: .value("Value", datum.value))
                    .foregroundStyle(by: .value("Series", datum.series))
                    .symbolSize(70)
                }
        }
        .chartYAxisLabel(figure.yAxisTitle ?? "")
        .chartLegend(.visible)
        .standardPlotStyle()
        .frame(minHeight: 285)
    }

    private var histogram: some View {
        Chart(categoryData) { datum in
            BarMark(x: .value("Interval", datum.label), y: .value("Frequency", datum.value), width: .ratio(1))
                .foregroundStyle(GRETheme.brightBlue.gradient)
        }
        .chartYAxisLabel(figure.yAxisTitle ?? "")
        .standardPlotStyle()
        .frame(minHeight: 270)
    }

    private var normalCurve: some View {
        Chart(coordinateData) { datum in
            LineMark(x: .value("x", datum.x), y: .value("Density", datum.value), series: .value("Series", datum.series))
                .foregroundStyle(by: .value("Series", datum.series))
                .lineStyle(StrokeStyle(lineWidth: 2.5))
        }
        .chartYAxis(.hidden)
        .chartLegend(.visible)
        .standardPlotStyle()
        .frame(minHeight: 270)
    }

    private var categoryData: [CategoryDatum] {
        figure.series.flatMap(categoryData(for:))
    }

    private func categoryData(for series: QuantFigureSeries) -> [CategoryDatum] {
        series.points.enumerated().compactMap { index, point in
            guard let label = point.label, let value = point.value else { return nil }
            return CategoryDatum(id: "\(series.name)-\(index)", series: series.name, label: label, value: value, colorIndex: index)
        }
    }

    private func pieColor(_ index: Int) -> Color {
        [GRETheme.brightBlue, GRETheme.teal, .orange, .purple, .pink, .indigo][index % 6]
    }

    private var coordinateData: [CoordinateDatum] {
        figure.series.flatMap { series in
            series.points.enumerated().compactMap { index, point in
                guard let x = point.x, let value = point.value else { return nil }
                return CoordinateDatum(id: "\(series.name)-\(index)", series: series.name, x: x, value: value)
            }
        }
    }

    private var accessibleTableText: String {
        let header = figure.headers?.joined(separator: ", ") ?? ""
        let rows = (figure.rows ?? []).map { $0.joined(separator: ", ") }.joined(separator: "; ")
        return "\(header). \(rows)"
    }
}

private struct CategoryDatum: Identifiable {
    let id: String
    let series: String
    let label: String
    let value: Double
    let colorIndex: Int
}

private struct CoordinateDatum: Identifiable {
    let id: String
    let series: String
    let x: Double
    let value: Double
}

private extension View {
    func standardPlotStyle() -> some View {
        chartPlotStyle { plot in
            plot
                .background(GRETheme.surface)
                .overlay(Rectangle().stroke(GRETheme.border, lineWidth: 0.7))
        }
    }
}

private struct BoxPlotFigure: View {
    let figure: QuantFigure

    var body: some View {
        VStack(spacing: 10) {
            ForEach(boxes, id: \.name) { box in
                HStack(spacing: 10) {
                    Text(box.name)
                        .font(.caption.bold())
                        .frame(width: 58, alignment: .trailing)
                    GeometryReader { proxy in
                        let width = max(1, proxy.size.width - 20)
                        let position: (Double) -> CGFloat = { value in
                            10 + width * (value - minimum) / max(1, maximum - minimum)
                        }
                        ZStack(alignment: .leading) {
                            Path { path in
                                let center = proxy.size.height / 2
                                path.move(to: CGPoint(x: position(box.low), y: center))
                                path.addLine(to: CGPoint(x: position(box.high), y: center))
                                for value in [box.low, box.high] {
                                    path.move(to: CGPoint(x: position(value), y: center - 11))
                                    path.addLine(to: CGPoint(x: position(value), y: center + 11))
                                }
                            }
                            .stroke(GRETheme.navy, lineWidth: 2)
                            Rectangle()
                                .fill(GRETheme.brightBlue.opacity(0.22))
                                .overlay(Rectangle().stroke(GRETheme.blue, lineWidth: 2))
                                .frame(width: max(2, position(box.q3) - position(box.q1)), height: 32)
                                .offset(x: position(box.q1), y: (proxy.size.height - 32) / 2)
                            Rectangle()
                                .fill(GRETheme.navy)
                                .frame(width: 2, height: 32)
                                .offset(x: position(box.median), y: (proxy.size.height - 32) / 2)
                        }
                    }
                    .frame(height: 48)
                }
            }
            HStack {
                Text(format(minimum))
                Spacer()
                Text("Five-number summaries")
                Spacer()
                Text(format(maximum))
            }
            .font(.caption2.monospacedDigit())
            .foregroundStyle(.secondary)
            .padding(.leading, 68)
        }
        .frame(minHeight: CGFloat(boxes.count * 58 + 34))
    }

    private var boxes: [(name: String, low: Double, q1: Double, median: Double, q3: Double, high: Double)] {
        figure.series.compactMap { series in
            guard let point = series.points.first,
                  let low = point.low, let q1 = point.q1, let median = point.median,
                  let q3 = point.q3, let high = point.high else { return nil }
            return (series.name, low, q1, median, q3, high)
        }
    }

    private var minimum: Double { boxes.map(\.low).min() ?? 0 }
    private var maximum: Double { boxes.map(\.high).max() ?? 1 }
    private func format(_ value: Double) -> String { value.formatted(.number.precision(.fractionLength(0...1))) }
}

private struct VennFigure: View {
    let figure: QuantFigure

    var body: some View {
        GeometryReader { proxy in
            let diameter = min(proxy.size.width * 0.48, proxy.size.height * 0.78)
            ZStack {
                Circle()
                    .fill(GRETheme.brightBlue.opacity(0.2))
                    .overlay(Circle().stroke(GRETheme.blue, lineWidth: 2))
                    .frame(width: diameter, height: diameter)
                    .position(x: proxy.size.width * 0.4, y: proxy.size.height * 0.53)
                Circle()
                    .fill(GRETheme.teal.opacity(0.18))
                    .overlay(Circle().stroke(GRETheme.teal, lineWidth: 2))
                    .frame(width: diameter, height: diameter)
                    .position(x: proxy.size.width * 0.6, y: proxy.size.height * 0.53)
                ForEach(figure.annotations ?? []) { annotation in
                    VStack(spacing: 1) {
                        Text(annotation.label).font(.caption2)
                        Text(annotation.value).font(.headline.monospacedDigit())
                    }
                    .multilineTextAlignment(.center)
                    .position(x: proxy.size.width * annotation.x, y: proxy.size.height * annotation.y)
                }
            }
        }
        .frame(minHeight: 285)
        .accessibilityLabel((figure.annotations ?? []).map { "\($0.label): \($0.value)" }.joined(separator: ", "))
    }
}
