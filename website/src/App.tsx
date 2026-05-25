import { useMemo, useState } from "react";
import { copy, Language, releaseUrl, releasesUrl, sourceUrl } from "./content";

function App() {
  const [language, setLanguage] = useState<Language>("en");
  const [logoOk, setLogoOk] = useState(true);
  const text = copy[language];
  const currentYear = useMemo(() => new Date().getFullYear(), []);

  return (
    <div className="site-shell">
      <header className="hero">
        <nav className="topbar" aria-label="Primary navigation">
          <a className="brand" href="#top" aria-label="Snappy Docs Convert home">
            {logoOk ? (
              <img src="/assets/logo.png" alt="" onError={() => setLogoOk(false)} />
            ) : (
              <span className="brand-mark">SD</span>
            )}
            <span>Snappy Docs Convert</span>
          </a>

          <div className="nav-links">
            <a href="#features">{text.nav.features}</a>
            <a href="#privacy">{text.nav.privacy}</a>
            <a href="#how">{text.nav.how}</a>
            <a href="#limitations">{text.nav.limitations}</a>
          </div>

          <div className="nav-actions">
            <div className="language-toggle" aria-label="Language">
              <button
                type="button"
                className={language === "en" ? "active" : ""}
                onClick={() => setLanguage("en")}
                aria-pressed={language === "en"}
              >
                EN
              </button>
              <button
                type="button"
                className={language === "vi" ? "active" : ""}
                onClick={() => setLanguage("vi")}
                aria-pressed={language === "vi"}
              >
                VI
              </button>
            </div>
            <a className="small-link" href={sourceUrl}>{text.nav.source}</a>
          </div>
        </nav>

        <section id="top" className="hero-grid">
          <div className="hero-copy">
            <p className="privacy-badge">{text.hero.proof}</p>
            <h1>{text.hero.title}</h1>
            <p className="hero-lead">{text.hero.subtitle}</p>
            <div className="cta-row">
              <a className="button primary" href={releaseUrl}>
                {text.hero.primaryCta}
              </a>
              <a className="button secondary" href={sourceUrl}>
                {text.hero.secondaryCta}
              </a>
            </div>
            <p className="release-note">
              <a href={releasesUrl}>{text.nav.download}</a> · {text.hero.releaseNote}
            </p>
          </div>

          <div className="product-card" aria-label="Snappy Docs Convert product summary">
            <div className="window-bar">
              <span />
              <span />
              <span />
            </div>
            <div className="tool-preview">
              <div>
                <strong>PDF + Images</strong>
                <small>Office / PDF / Images</small>
              </div>
              <div className="status-pill">Local</div>
            </div>
            <div className="queue-lines">
              <span />
              <span />
              <span />
            </div>
            <div className="mini-grid">
              {text.trust.map((item) => (
                <div key={item}>{item}</div>
              ))}
            </div>
          </div>
        </section>
      </header>

      <main>
        <section id="features" className="section">
          <div className="section-heading">
            <span>01</span>
            <h2>{text.featuresTitle}</h2>
          </div>
          <div className="feature-grid">
            {text.features.map((feature) => (
              <article className="feature-card" key={feature.title}>
                <h3>{feature.title}</h3>
                <p>{feature.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="privacy" className="section split-section">
          <div className="section-heading">
            <span>02</span>
            <h2>{text.privacyTitle}</h2>
          </div>
          <div className="privacy-list">
            {text.privacy.map((item) => (
              <article key={item.title}>
                <h3>{item.title}</h3>
                <p>{item.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="how" className="section">
          <div className="section-heading">
            <span>03</span>
            <h2>{text.howTitle}</h2>
          </div>
          <div className="steps">
            {text.how.map((step) => (
              <article key={step.title}>
                <h3>{step.title}</h3>
                <p>{step.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="limitations" className="section limit-section">
          <div>
            <div className="section-heading">
              <span>04</span>
              <h2>{text.limitationsTitle}</h2>
            </div>
            <ul>
              {text.limitations.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
          <div className="download-panel">
            <h3>Snappy Docs Convert</h3>
            <p>{text.footer.line}</p>
            <a className="button primary" href={releaseUrl}>{text.footer.download}</a>
            <a className="button secondary" href={sourceUrl}>{text.footer.source}</a>
          </div>
        </section>
      </main>

      <footer>
        <span>© {currentYear} Snappy Docs Convert</span>
        <span>{text.footer.line}</span>
      </footer>
    </div>
  );
}

export default App;
