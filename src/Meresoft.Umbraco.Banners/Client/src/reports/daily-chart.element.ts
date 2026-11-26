import { css, customElement, html, nothing, property, svg } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { ReportDayModel } from "../api/types.gen.js";

const WIDTH = 720;
const LEFT = 48;
const RIGHT = 8;
const TOP = 8;
const IMPRESSIONS_HEIGHT = 140;
const GAP = 22;
const CLICKS_HEIGHT = 64;
const BOTTOM = 22;
const HEIGHT = TOP + IMPRESSIONS_HEIGHT + GAP + CLICKS_HEIGHT + BOTTOM;

/** The smallest 1, 2 or 5 times a power of ten that is not below the value, so the axis ends on a round number. */
function niceMax(value: number): number {
  if (value <= 1) {
    return 1;
  }
  const power = 10 ** Math.floor(Math.log10(value));
  const fraction = value / power;
  return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power;
}

/**
 * Daily impressions and clicks as two small bar charts that share the day axis, drawn as plain SVG.
 * Impressions and clicks differ by orders of magnitude, so each has its own scale instead of one shared axis.
 * Days with imported numbers are hatched. The same numbers are in the table of the reports view, which is the text alternative.
 */
@customElement("meresoft-daily-chart")
export class MeresoftDailyChartElement extends UmbLitElement {
  /** One entry per UTC day of the period. */
  @property({ attribute: false })
  days: ReportDayModel[] = [];

  override render() {
    if (this.days.length === 0) {
      return nothing;
    }

    const plotWidth = WIDTH - LEFT - RIGHT;
    const step = plotWidth / this.days.length;
    const barWidth = Math.max(1, step * 0.72);
    const impressionsMax = niceMax(Math.max(...this.days.map((d) => d.impressions)));
    const clicksMax = niceMax(Math.max(...this.days.map((d) => d.clicks)));
    const clicksTop = TOP + IMPRESSIONS_HEIGHT + GAP;
    const labelEvery = Math.ceil(this.days.length / 8);
    const total = (key: "impressions" | "clicks") => this.days.reduce((sum, d) => sum + d[key], 0).toLocaleString();
    const summary = this.localize.term("meresoftBanners_chartSummary", this.days[0].day, this.days[this.days.length - 1].day, total("impressions"), total("clicks"));

    return html`
      <svg viewBox="0 0 ${WIDTH} ${HEIGHT}" role="img" aria-labelledby="chart-title" preserveAspectRatio="xMidYMid meet">
        <title id="chart-title">${summary}</title>
        <defs>
          <pattern id="imported" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect width="6" height="6" class="hatch-ground"></rect>
            <rect width="2.5" height="6" class="hatch-line"></rect>
          </pattern>
        </defs>
        ${this.#panel(this.localize.term("meresoftBanners_impressions"), TOP, IMPRESSIONS_HEIGHT, impressionsMax)}
        ${this.#panel(this.localize.term("meresoftBanners_clicks"), clicksTop, CLICKS_HEIGHT, clicksMax)}
        ${this.days.map((day, index) => {
          const x = LEFT + index * step + (step - barWidth) / 2;
          const impressionsHeight = (day.impressions / impressionsMax) * IMPRESSIONS_HEIGHT;
          const clicksHeight = (day.clicks / clicksMax) * CLICKS_HEIGHT;
          const tip = this.localize.term("meresoftBanners_chartTip", day.day, day.impressions.toLocaleString(), day.clicks.toLocaleString());
          return svg`
            <g class=${day.imported ? "day imported" : "day"}>
              <title>${tip}${day.imported ? ` (${this.localize.term("meresoftBanners_imported")})` : ""}</title>
              <rect class="hit" x=${LEFT + index * step} y=${TOP} width=${step} height=${HEIGHT - TOP - BOTTOM}></rect>
              <rect class="impressions" fill=${day.imported ? "url(#imported)" : nothing} x=${x} y=${TOP + IMPRESSIONS_HEIGHT - impressionsHeight} width=${barWidth} height=${impressionsHeight}></rect>
              <rect class="clicks" fill=${day.imported ? "url(#imported)" : nothing} x=${x} y=${clicksTop + CLICKS_HEIGHT - clicksHeight} width=${barWidth} height=${clicksHeight}></rect>
              ${index % labelEvery === 0
                ? svg`<text class="tick" x=${LEFT + index * step + step / 2} y=${HEIGHT - 6} text-anchor="middle">${day.day.slice(5)}</text>`
                : nothing}
            </g>
          `;
        })}
      </svg>
      <p class="legend">
        <span class="swatch impressions"></span>${this.localize.term("meresoftBanners_impressions")}
        <span class="swatch clicks"></span>${this.localize.term("meresoftBanners_clicks")}
        ${this.days.some((d) => d.imported)
          ? html`<span class="swatch imported"></span>${this.localize.term("meresoftBanners_importedDays")}`
          : nothing}
      </p>
    `;
  }

  #panel(label: string, top: number, height: number, max: number) {
    const lines = [0, max / 2, max];
    return svg`
      <g class="panel" aria-hidden="true">
        <text class="axis-label" x=${LEFT} y=${top - 1}>${label}</text>
        ${lines.map((value) => {
          const y = top + height - (value / max) * height;
          return svg`
            <line class="grid" x1=${LEFT} x2=${WIDTH - RIGHT} y1=${y} y2=${y}></line>
            <text class="tick" x=${LEFT - 6} y=${y + 3} text-anchor="end">${Math.round(value).toLocaleString()}</text>
          `;
        })}
      </g>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }
      svg {
        display: block;
        width: 100%;
        height: auto;
        overflow: visible;
      }
      text {
        font-family: inherit;
        font-size: 10px;
        fill: var(--uui-color-text-alt);
      }
      .axis-label {
        font-size: 11px;
        font-weight: 700;
        fill: var(--uui-color-text);
      }
      .grid {
        stroke: var(--uui-color-border);
        stroke-width: 1;
      }
      .hit {
        fill: transparent;
      }
      .day:hover .hit {
        fill: var(--uui-color-surface-emphasis);
      }
      /* Imported days take their hatched fill from the fill attribute: a url(#id) in CSS would not find the pattern inside the shadow root. */
      .day:not(.imported) .impressions {
        fill: var(--uui-color-interactive);
      }
      .day:not(.imported) .clicks {
        fill: var(--uui-color-positive);
      }
      .imported .impressions {
        stroke: var(--uui-color-interactive);
        stroke-width: 1;
      }
      .imported .clicks {
        stroke: var(--uui-color-positive);
        stroke-width: 1;
      }
      .hatch-ground {
        fill: var(--uui-color-surface);
      }
      .hatch-line {
        fill: var(--uui-color-text-alt);
      }
      .legend {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2) var(--uui-size-space-5);
        margin: var(--uui-size-space-3) 0 0;
        color: var(--uui-color-text-alt);
      }
      .swatch {
        display: inline-block;
        width: 0.9em;
        height: 0.9em;
        margin-right: var(--uui-size-space-2);
        vertical-align: -0.1em;
      }
      .swatch.impressions {
        background: var(--uui-color-interactive);
      }
      .swatch.clicks {
        background: var(--uui-color-positive);
      }
      .swatch.imported {
        background: repeating-linear-gradient(45deg, var(--uui-color-text-alt) 0 2px, var(--uui-color-surface) 2px 5px);
        border: 1px solid var(--uui-color-text-alt);
      }
    `,
  ];
}

export default MeresoftDailyChartElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-daily-chart": MeresoftDailyChartElement;
  }
}
