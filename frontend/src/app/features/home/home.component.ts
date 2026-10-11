import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { LucideArrowRight, LucideSearch, LucideUsersRound } from '@lucide/angular';
import { catchError, finalize, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { PropertyService } from '../../core/services/property.service';
import { PropertyCardComponent } from '../../shared/components/property-card/property-card.component';

@Component({
  selector: 'app-home',
  imports: [RouterLink, PropertyCardComponent, LucideArrowRight, LucideSearch, LucideUsersRound],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hero">
      <div class="container hero-grid">
        <div class="hero-copy">
          <p class="eyebrow">Property, thoughtfully managed</p>
          <h1><span>A better place</span><span>begins with better care.</span></h1>
          <p class="intro">
            Distinctive homes, clear guidance, and a management team that stays present long after
            the keys change hands.
          </p>
          <div class="actions">
            <a routerLink="/properties" class="btn btn-primary"
              ><svg lucideSearch></svg>Browse properties</a
            ><a routerLink="/about" class="btn btn-secondary"
              ><svg lucideUsersRound></svg>Meet our team</a
            >
          </div>
          <div class="trust">
            <span><b>12+</b> years of local care</span><span><b>96%</b> enquiry response rate</span
            ><span><b>4.9</b> resident rating</span>
          </div>
        </div>
        <div class="hero-image">
          <img
            src="/assets/imperial-estate-hero.webp"
            alt="Imperial Estates manor and landscaped grounds"
            width="1672"
            height="941"
            fetchpriority="high"
          />
          <div class="note">
            <span>Now available</span><strong>Riverside Collection</strong
            ><a routerLink="/properties">View homes <svg lucideArrowRight></svg></a>
          </div>
        </div>
      </div>
    </section>
    <section class="section featured">
      <div class="container">
        <div class="section-heading featured-heading">
          <p class="eyebrow">Selected residences</p>
          <h2>Homes that make room for life.</h2>
          <a routerLink="/properties">View every property <svg lucideArrowRight></svg></a>
        </div>
        <div class="grid">
          @for (property of featured(); track property.id) {
            <app-property-card [property]="property" />
          } @empty {
            @if (loading()) {
              <div class="featured-empty" role="status" aria-busy="true">
                Loading featured homes…
              </div>
            } @else if (error()) {
              <div class="featured-empty" role="alert">
                <p>Featured homes could not be loaded.</p>
                <button class="btn btn-secondary" (click)="reload()">Try again</button>
              </div>
            } @else {
              <div class="featured-empty">
                <svg lucideSearch></svg>
                <div>
                  <h3>New residences are being prepared</h3>
                  <p>
                    There are no featured homes to display right now. Check the full property
                    collection for the latest availability.
                  </p>
                </div>
              </div>
            }
          }
        </div>
      </div>
    </section>
    <section class="section highlights">
      <div class="container highlight-grid">
        <div>
          <p class="eyebrow">A considered approach</p>
          <h2>Calm guidance at every turn.</h2>
          <p>
            We combine local judgement with attentive service, so property decisions feel clearer
            and homes are cared for consistently.
          </p>
        </div>
        <div class="benefits">
          <article>
            <span>01</span>
            <div>
              <h3>Curated, never crowded</h3>
              <p>
                Every available home is checked, accurately presented, and ready for a genuine
                conversation.
              </p>
            </div>
          </article>
          <article>
            <span>02</span>
            <div>
              <h3>People who stay accountable</h3>
              <p>
                A dedicated estate team manages the details from first enquiry through the complete
                tenancy.
              </p>
            </div>
          </article>
          <article>
            <span>03</span>
            <div>
              <h3>Clear by design</h3>
              <p>Transparent status, pricing, and next steps help you move with confidence.</p>
            </div>
          </article>
        </div>
      </div>
    </section>
    <section class="section process">
      <div class="container">
        <div class="section-heading">
          <p class="eyebrow">Simple from the start</p>
          <h2>Your next home, in three clear steps.</h2>
        </div>
        <div class="steps">
          <article>
            <b>01</b>
            <h3>Explore</h3>
            <p>Browse verified, currently available properties and filter around what matters.</p>
          </article>
          <article>
            <b>02</b>
            <h3>Connect</h3>
            <p>Send an enquiry and our team will arrange the right conversation or viewing.</p>
          </article>
          <article>
            <b>03</b>
            <h3>Settle in</h3>
            <p>We handle the handover carefully and remain available throughout your tenancy.</p>
          </article>
        </div>
      </div>
    </section>
    <section class="cta">
      <div class="container">
        <p class="eyebrow">Begin your search</p>
        <h2>There is more to a home than an address.</h2>
        <a routerLink="/properties" class="btn">Find yours</a>
      </div>
    </section>
  `,
  styles: [
    `
      .hero {
        padding: 52px 0 64px;
        background: linear-gradient(116deg, var(--paper) 61%, var(--surface-muted) 61%);
      }
      .hero-grid {
        display: grid;
        grid-template-columns: minmax(0, 0.92fr) minmax(0, 1.08fr);
        align-items: center;
        gap: clamp(64px, 6vw, 112px);
      }
      .hero-copy {
        max-width: 790px;
        padding: 26px 0;
      }
      .actions {
        display: flex;
        gap: 12px;
        margin: 28px 0 44px;
      }
      .actions .btn {
        min-height: 44px;
        padding-inline: 17px;
      }
      .trust {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        max-width: 680px;
        border-top: 1px solid var(--border);
        padding-top: 22px;
        color: var(--muted);
        font-size: 0.72rem;
      }
      .trust span {
        display: grid;
        gap: 3px;
        padding: 0 22px;
        border-left: 1px solid var(--border);
      }
      .trust span:first-child {
        padding-left: 0;
        border-left: 0;
      }
      .trust b {
        color: var(--ink);
        font-size: 1.2rem;
        line-height: 1;
      }
      .hero-image {
        position: relative;
        height: clamp(540px, 66vh, 720px);
      }
      .hero-image > img {
        width: 100%;
        height: 100%;
        object-fit: cover;
        border-radius: clamp(120px, 10vw, 180px) clamp(120px, 10vw, 180px) 28px 28px;
      }
      .note {
        position: absolute;
        left: -22px;
        bottom: 28px;
        display: grid;
        gap: 4px;
        min-width: 190px;
        padding: 18px 20px;
        border: 1px solid var(--border);
        border-radius: 14px;
        background: color-mix(in srgb, var(--surface) 94%, transparent);
        box-shadow: var(--shadow-lg);
        backdrop-filter: blur(14px);
      }
      .note span {
        font-size: 0.65rem;
        text-transform: uppercase;
        color: var(--bronze);
        letter-spacing: 0.1em;
      }
      .note a {
        font-size: 0.78rem;
        margin-top: 8px;
        font-weight: 700;
        display: inline-flex;
        align-items: center;
        gap: 6px;
      }
      .featured .section-heading {
        max-width: 840px;
      }
      .featured-heading h2 {
        margin-bottom: 0;
      }
      .featured-heading > a {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        margin-top: 12px;
        font-weight: 700;
      }
      .note a svg,
      .featured-heading > a svg {
        width: 16px;
        height: 16px;
      }
      .grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 22px;
      }
      .featured-empty {
        grid-column: 1 / -1;
        display: flex;
        align-items: center;
        gap: 18px;
        min-height: 112px;
        padding: 24px 28px;
        border: 1px solid var(--contrast-border);
        border-radius: var(--radius-md);
        background: color-mix(in srgb, var(--contrast-surface) 94%, var(--forest) 6%);
      }
      .featured-empty > svg {
        width: 22px;
        height: 22px;
        flex: 0 0 auto;
        color: var(--forest);
      }
      .featured-empty h3 {
        margin: 0 0 5px;
        font-size: 1rem;
      }
      .featured-empty p {
        max-width: 720px;
        margin: 0;
        color: var(--contrast-muted);
        font-size: 0.82rem;
      }
      .highlights {
        background: var(--surface-soft);
      }
      .highlight-grid {
        display: grid;
        grid-template-columns: 0.8fr 1.2fr;
        gap: 90px;
      }
      .highlight-grid > div:first-child p:not(.eyebrow) {
        color: var(--muted);
        font-size: 1.05rem;
      }
      .benefits article {
        display: grid;
        grid-template-columns: 50px 1fr;
        gap: 18px;
        padding: 24px 0;
        border-bottom: 1px solid var(--border);
      }
      .benefits article > span {
        color: var(--bronze);
        font-size: 0.75rem;
      }
      .benefits h3 {
        margin-bottom: 7px;
      }
      .benefits p {
        margin: 0;
        color: var(--muted);
      }
      .steps {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 1px;
        background: var(--border);
        border: 1px solid var(--border);
        border-radius: var(--radius-md);
        overflow: hidden;
      }
      .steps article {
        background: var(--surface);
        padding: 36px;
      }
      .steps b {
        color: var(--bronze);
        font-size: 0.75rem;
      }
      .steps h3 {
        margin-top: 42px;
      }
      .steps p {
        color: var(--muted);
        margin-bottom: 0;
      }
      .cta {
        text-align: center;
        background: var(--contrast-surface);
        color: var(--contrast-text);
        padding: 100px 0;
      }
      .cta h2 {
        max-width: 760px;
        margin: 18px auto 30px;
      }
      .cta .btn {
        background: var(--contrast-text);
        color: var(--forest);
      }
      @media (max-width: 900px) {
        .hero {
          background: var(--paper);
        }
        .hero-grid {
          grid-template-columns: 1fr;
          gap: 34px;
        }
        .hero-image {
          height: 500px;
        }
        .note {
          left: 14px;
        }
        .grid {
          grid-template-columns: repeat(2, 1fr);
        }
        .highlight-grid {
          grid-template-columns: 1fr;
          gap: 30px;
        }
      }
      @media (max-width: 620px) {
        .hero {
          padding-top: 0;
        }
        .hero-grid {
          gap: 10px;
        }
        .hero-copy {
          padding-top: 42px;
        }
        .hero-image {
          height: 420px;
        }
        .hero-image > img {
          border-radius: 130px 130px 18px 18px;
        }
        .trust {
          grid-template-columns: repeat(3, minmax(120px, 1fr));
          overflow-x: auto;
        }
        .grid,
        .steps {
          grid-template-columns: 1fr;
        }
        .featured-empty {
          align-items: flex-start;
          padding: 20px;
        }
        .actions {
          align-items: stretch;
          flex-direction: column;
        }
        .steps article h3 {
          margin-top: 20px;
        }
      }
    `,
  ],
})
export class HomeComponent {
  private properties = inject(PropertyService);
  readonly loading = signal(true);
  readonly error = signal(false);
  private readonly reload$ = new Subject<void>();
  readonly featured = toSignal(
    this.reload$.pipe(
      startWith(undefined),
      switchMap(() => {
        this.loading.set(true);
        this.error.set(false);
        return this.properties.featured().pipe(
          tap(() => this.loading.set(false)),
          catchError(() => {
            this.error.set(true);
            return of([]);
          }),
          finalize(() => this.loading.set(false)),
        );
      }),
    ),
    {
      initialValue: [],
    },
  );
  reload() {
    this.reload$.next();
  }
}
