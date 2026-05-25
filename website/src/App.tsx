import { useEffect, useMemo, useState } from "react";
import {
  appName,
  copy,
  discordUrl,
  installerUrl,
  Language,
  portableUrl,
  releaseTagUrl,
  releasesUrl,
  ScreenshotCopy,
  sourceUrl,
} from "./content";

type ThemePreference = "system" | "light" | "dark";
type ResolvedTheme = "light" | "dark";

const resolveTheme = (preference: ThemePreference): ResolvedTheme => {
  if (preference === "light" || preference === "dark") {
    return preference;
  }

  if (typeof window === "undefined") {
    return "light";
  }

  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
};

const getInitialThemePreference = (): ThemePreference => {
  if (typeof window === "undefined") {
    return "system";
  }

  const saved = window.localStorage.getItem("snappy-theme");
  if (saved === "system" || saved === "light" || saved === "dark") {
    return saved;
  }

  return "system";
};

const getInitialLanguage = (): Language => {
  if (typeof window === "undefined") {
    return "en";
  }

  const saved = window.localStorage.getItem("snappy-language");
  if (saved === "en" || saved === "vi") {
    return saved;
  }

  return window.navigator.language.toLowerCase().startsWith("vi") ? "vi" : "en";
};

function ScreenshotCard({ item }: { item: ScreenshotCopy }) {
  const [imageMissing, setImageMissing] = useState(false);

  return (
    <article className="screenshot-card">
      {!imageMissing ? (
        <img src={item.src} alt={item.title} onError={() => setImageMissing(true)} />
      ) : (
        <div className="screenshot-placeholder" aria-label={item.title}>
          <span>{item.title}</span>
          <small>PNG placeholder</small>
        </div>
      )}
      <div>
        <h3>{item.title}</h3>
        <p>{item.text}</p>
      </div>
    </article>
  );
}

function App() {
  const [language, setLanguage] = useState<Language>(getInitialLanguage);
  const [themePreference, setThemePreference] = useState<ThemePreference>(getInitialThemePreference);
  const [logoOk, setLogoOk] = useState(true);
  const text = copy[language];
  const currentYear = useMemo(() => new Date().getFullYear(), []);
  const resolvedTheme = resolveTheme(themePreference);

  useEffect(() => {
    document.documentElement.dataset.theme = resolvedTheme;
    document.documentElement.lang = language;
    window.localStorage.setItem("snappy-theme", themePreference);
    window.localStorage.setItem("snappy-language", language);
  }, [language, resolvedTheme, themePreference]);

  useEffect(() => {
    if (themePreference !== "system") {
      return;
    }

    const query = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => {
      document.documentElement.dataset.theme = resolveTheme("system");
    };
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, [themePreference]);

  return (
    <div className="site-shell">
      <header className="hero" id="top">
        <nav className="topbar" aria-label="Primary navigation">
          <a className="brand" href="#top" aria-label={`${appName} home`}>
            {logoOk ? (
              <img src="/assets/logo.png" alt="" onError={() => setLogoOk(false)} />
            ) : (
              <span className="brand-mark">SD</span>
            )}
            <span>{appName}</span>
          </a>

          <div className="nav-links">
            <a href="#download">{text.nav.download}</a>
            <a href="#local">{text.nav.local}</a>
            <a href="#features">{text.nav.features}</a>
            <a href="#toolbox">{text.nav.toolbox}</a>
            <a href="#screenshots">{text.nav.screenshots}</a>
            <a href="#support">{text.nav.support}</a>
          </div>

          <div className="nav-actions">
            <div className="toggle-group" aria-label={text.controls.language}>
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
            <div className="toggle-group theme-group" aria-label="Theme">
              <button
                type="button"
                className={themePreference === "system" ? "active" : ""}
                onClick={() => setThemePreference("system")}
                aria-pressed={themePreference === "system"}
              >
                {text.controls.system}
              </button>
              <button
                type="button"
                className={themePreference === "light" ? "active" : ""}
                onClick={() => setThemePreference("light")}
                aria-pressed={themePreference === "light"}
              >
                {text.controls.light}
              </button>
              <button
                type="button"
                className={themePreference === "dark" ? "active" : ""}
                onClick={() => setThemePreference("dark")}
                aria-pressed={themePreference === "dark"}
              >
                {text.controls.dark}
              </button>
            </div>
          </div>
        </nav>

        <section className="hero-grid">
          <div className="hero-copy">
            <p className="eyebrow">{text.hero.eyebrow}</p>
            <h1>{text.hero.title}</h1>
            <p className="hero-lead">{text.hero.subtitle}</p>
            <p className="privacy-badge">{text.hero.proof}</p>
            <div className="cta-row">
              <a className="button primary" href={installerUrl}>
                {text.hero.installerCta}
              </a>
              <a className="button secondary" href={portableUrl}>
                {text.hero.portableCta}
              </a>
              <a className="button text-button" href={releaseTagUrl}>
                {text.hero.releaseCta}
              </a>
            </div>
            <p className="release-note">{text.hero.releaseNote}</p>
          </div>

          <div className="product-panel" aria-label={`${appName} preview`}>
            <div className="product-panel-head">
              <span />
              <span />
              <span />
            </div>
            <div className="product-panel-body">
              <img src="/assets/logo.png" alt="" onError={(event) => (event.currentTarget.style.display = "none")} />
              <div>
                <strong>{appName}</strong>
                <small>{text.footer.line}</small>
              </div>
            </div>
            <div className="panel-list">
              {text.stats.map((stat) => (
                <div key={stat.label}>
                  <strong>{stat.value}</strong>
                  <span>{stat.label}</span>
                </div>
              ))}
            </div>
          </div>
        </section>
      </header>

      <main>
        <section id="download" className="section download-section">
          <div>
            <p className="section-kicker">01 / {text.nav.download}</p>
            <h2>{text.download.title}</h2>
            <p>{text.download.text}</p>
            <p className="muted">{text.download.betaNote}</p>
          </div>
          <div className="download-actions" aria-label="Download links">
            <a className="button primary" href={installerUrl}>
              {text.download.installer}
            </a>
            <a className="button secondary" href={portableUrl}>
              {text.download.portable}
            </a>
            <a className="button secondary" href={releasesUrl}>
              {text.download.allReleases}
            </a>
          </div>
        </section>

        <section id="local" className="section">
          <div className="section-heading">
            <p className="section-kicker">02 / {text.nav.local}</p>
            <h2>{text.localTitle}</h2>
          </div>
          <div className="card-grid three">
            {text.localCards.map((card) => (
              <article className="info-card" key={card.title}>
                <h3>{card.title}</h3>
                <p>{card.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="features" className="section">
          <div className="section-heading">
            <p className="section-kicker">03 / {text.nav.features}</p>
            <h2>{text.featuresTitle}</h2>
            <p>{text.featuresIntro}</p>
          </div>
          <div className="card-grid">
            {text.features.map((feature) => (
              <article className="info-card" key={feature.title}>
                <h3>{feature.title}</h3>
                <p>{feature.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="toolbox" className="section toolbox-section">
          <div className="section-heading">
            <p className="section-kicker">04 / {text.nav.toolbox}</p>
            <h2>{text.toolboxTitle}</h2>
            <p>{text.toolboxIntro}</p>
          </div>
          <div className="toolbox-grid">
            {text.toolbox.map((tool) => (
              <article key={tool.title}>
                <span>PDF</span>
                <h3>{tool.title}</h3>
                <p>{tool.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="screenshots" className="section">
          <div className="section-heading">
            <p className="section-kicker">05 / {text.nav.screenshots}</p>
            <h2>{text.screenshotsTitle}</h2>
            <p>{text.screenshotsIntro}</p>
          </div>
          <div className="screenshots-grid">
            {text.screenshots.map((item) => (
              <ScreenshotCard item={item} key={item.title} />
            ))}
          </div>
          <div className="demo-video-card">
            <div>
              <h3>{language === "vi" ? "Video demo app" : "Desktop app demo"}</h3>
              <p>{language === "vi" ? "Video desktop app với dữ liệu demo cục bộ." : "Desktop app video recorded with local synthetic demo inputs."}</p>
            </div>
            <video controls preload="metadata" src="/demo/app-demo.mp4" />
          </div>
          <p className="muted website-demo-link">
            {language === "vi" ? "Demo website vẫn có trong bộ asset, nhưng trang này ưu tiên demo app." : "Website demo assets remain available, but this section prioritizes the desktop app."}
          </p>
        </section>

        <section className="section split-section">
          <div id="open-source">
            <p className="section-kicker">06 / {text.nav.source}</p>
            <h2>{text.openSourceTitle}</h2>
          </div>
          <div className="stacked-cards">
            {text.openSource.map((item) => (
              <article className="info-card" key={item.title}>
                <h3>{item.title}</h3>
                <p>{item.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="roadmap" className="section">
          <div className="section-heading">
            <p className="section-kicker">07 / {text.nav.roadmap}</p>
            <h2>{text.roadmapTitle}</h2>
          </div>
          <div className="roadmap-list">
            {text.roadmap.map((item) => (
              <article key={item.title}>
                <h3>{item.title}</h3>
                <p>{item.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="faq" className="section faq-section">
          <div className="section-heading">
            <p className="section-kicker">08 / {text.nav.faq}</p>
            <h2>{text.faqTitle}</h2>
          </div>
          <div className="faq-list">
            {text.faq.map((item) => (
              <article key={item.question}>
                <h3>{item.question}</h3>
                <p>{item.answer}</p>
              </article>
            ))}
          </div>
        </section>

        <section id="support" className="section support-section">
          <div>
            <p className="section-kicker">09 / {text.nav.support}</p>
            <h2>{text.supportTitle}</h2>
            <p>{text.supportText}</p>
          </div>
          <div className="support-actions">
            <a className="button primary" href={discordUrl}>
              {text.discordCta}
            </a>
            <a className="button secondary" href={sourceUrl}>
              {text.hero.sourceCta}
            </a>
            <a className="button secondary" href={releaseTagUrl}>
              {text.hero.releaseCta}
            </a>
          </div>
        </section>
      </main>

      <footer>
        <span>© {currentYear} {appName}</span>
        <span>{text.footer.line}</span>
        <span>{text.footer.license}</span>
        <a href={sourceUrl}>{text.nav.source}</a>
      </footer>
    </div>
  );
}

export default App;
