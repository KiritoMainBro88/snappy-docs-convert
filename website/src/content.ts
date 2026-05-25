export type Language = "en" | "vi";

export type CardCopy = {
  title: string;
  text: string;
};

export type FaqCopy = {
  question: string;
  answer: string;
};

export type ScreenshotCopy = {
  title: string;
  text: string;
  src: string;
};

export type SiteCopy = {
  nav: {
    download: string;
    local: string;
    features: string;
    toolbox: string;
    screenshots: string;
    roadmap: string;
    faq: string;
    support: string;
    source: string;
  };
  controls: {
    language: string;
    system: string;
    light: string;
    dark: string;
  };
  hero: {
    eyebrow: string;
    title: string;
    subtitle: string;
    proof: string;
    installerCta: string;
    portableCta: string;
    releaseCta: string;
    sourceCta: string;
    releaseNote: string;
  };
  download: {
    title: string;
    text: string;
    installer: string;
    portable: string;
    allReleases: string;
    betaNote: string;
  };
  stats: Array<{ value: string; label: string }>;
  localTitle: string;
  localCards: CardCopy[];
  featuresTitle: string;
  featuresIntro: string;
  features: CardCopy[];
  toolboxTitle: string;
  toolboxIntro: string;
  toolbox: CardCopy[];
  screenshotsTitle: string;
  screenshotsIntro: string;
  screenshots: ScreenshotCopy[];
  openSourceTitle: string;
  openSource: CardCopy[];
  roadmapTitle: string;
  roadmap: CardCopy[];
  faqTitle: string;
  faq: FaqCopy[];
  supportTitle: string;
  supportText: string;
  discordCta: string;
  footer: {
    line: string;
    license: string;
  };
};

export const appName = "kmb file tools";
export const releaseTagUrl =
  "https://github.com/KiritoMainBro88/snappy-docs-convert/releases/tag/v0.1.0-beta.2";
export const releasesUrl = "https://github.com/KiritoMainBro88/snappy-docs-convert/releases";
export const installerUrl =
  "https://github.com/KiritoMainBro88/snappy-docs-convert/releases/download/v0.1.0-beta.2/kmb-file-tools-setup-win-x64-v0.1.0-beta.2.exe";
export const portableUrl =
  "https://github.com/KiritoMainBro88/snappy-docs-convert/releases/download/v0.1.0-beta.2/kmb-file-tools-portable-win-x64-v0.1.0-beta.2.zip";
export const sourceUrl = "https://github.com/KiritoMainBro88/snappy-docs-convert";
export const discordUrl = "https://discord.gg/kZ3U36ncun";

export const copy: Record<Language, SiteCopy> = {
  en: {
    nav: {
      download: "Download",
      local: "Local-only",
      features: "Features",
      toolbox: "PDF Toolbox",
      screenshots: "Screenshots",
      roadmap: "Roadmap",
      faq: "FAQ",
      support: "Support",
      source: "Source",
    },
    controls: {
      language: "Language",
      system: "System",
      light: "Light",
      dark: "Dark",
    },
    hero: {
      eyebrow: "Free and open-source Windows desktop app",
      title: "kmb file tools converts documents without uploading your files.",
      subtitle:
        "Convert Office documents, PDFs, images, pages, and slides locally on your PC. The website is only a download and project page for kmb file tools.",
      proof: "No upload · No telemetry · No account · MIT licensed",
      installerCta: "Download installer",
      portableCta: "Download portable ZIP",
      releaseCta: "View GitHub Release",
      sourceCta: "View source",
      releaseNote:
        "Public beta v0.1.0-beta.2. Unsigned builds may show a Windows SmartScreen warning.",
    },
    download: {
      title: "Official free downloads",
      text:
        "Download the installer or portable ZIP from GitHub Releases. Official downloads are free. Please avoid misleading paid repackages; support the original project.",
      installer: "Windows Installer EXE",
      portable: "Portable ZIP",
      allReleases: "View all releases",
      betaNote:
        "Microsoft Office is optional for best DOCX/PPTX fidelity. LibreOffice is an optional fallback and is not bundled.",
    },
    stats: [
      { value: "Local", label: "Desktop processing" },
      { value: "Free", label: "Community-friendly" },
      { value: "MIT", label: "Open-source license" },
      { value: "EN/VI", label: "Bilingual UI" },
    ],
    localTitle: "Why local-only matters",
    localCards: [
      {
        title: "Your files stay on your machine",
        text:
          "The desktop app processes documents locally. The website does not process your files and has no upload form.",
      },
      {
        title: "No telemetry",
        text:
          "No analytics scripts, tracking pixels, crash reporters, or account system are included in the website.",
      },
      {
        title: "Clear dependency behavior",
        text:
          "Use Microsoft Office when installed. If you do not have Office, the app guides you to install LibreOffice from the official site.",
      },
    ],
    featuresTitle: "Document conversion features",
    featuresIntro:
      "A practical Windows utility for common document workflows, designed to stay understandable instead of becoming a cloud dashboard.",
    features: [
      {
        title: "Office documents to PDF",
        text: "Convert DOC, DOCX, RTF, PPT, and PPTX through local Microsoft Office when available.",
      },
      {
        title: "LibreOffice fallback guidance",
        text: "Use LibreOffice headless conversion for supported formats when Microsoft Office is not available.",
      },
      {
        title: "PDF to PNG, JPEG, WebP",
        text: "Render real PDF pages to image files. No screenshots and no browser upload flow.",
      },
      {
        title: "Batch conversion",
        text: "Queue multiple files, pick an output folder, and keep partial success when one file fails.",
      },
      {
        title: "Engine setup status",
        text: "See Word, PowerPoint, LibreOffice, and PDF renderer availability before starting work.",
      },
      {
        title: "English and Vietnamese",
        text: "The desktop app and website detect system/browser language, then remember your choice.",
      },
    ],
    toolboxTitle: "PDF Toolbox V1",
    toolboxIntro:
      "Local PDF utilities for everyday cleanup tasks. No OCR, signing, encryption, or cloud compression in this beta.",
    toolbox: [
      { title: "Merge PDFs", text: "Combine several PDF files into one output file." },
      { title: "Split PDF", text: "Export each page as a separate PDF file." },
      { title: "Extract pages", text: "Create a new PDF from ranges such as 1,3-5,8." },
      { title: "Rotate pages", text: "Rotate all pages or selected page ranges." },
      { title: "Images to PDF", text: "Build one PDF from PNG, JPEG, or WebP images." },
    ],
    screenshotsTitle: "App screenshots",
    screenshotsIntro:
      "Demo screenshots and short videos show the website and desktop app flow. The site keeps working if media is not present.",
    screenshots: [
      {
        title: "App home",
        text: "Files, target, engine, output folder, and progress in one focused page.",
        src: "/demo/home-light-en.png",
      },
      {
        title: "PDF tools",
        text: "Merge, split, extract, rotate, and images-to-PDF actions separated from conversion.",
        src: "/demo/home-light-vi.png",
      },
      {
        title: "Website dark mode",
        text: "Dark landing page variant for release notes, social sharing, and user guides.",
        src: "/demo/home-dark-en.png",
      },
    ],
    openSourceTitle: "Open source and community",
    openSource: [
      {
        title: "MIT licensed",
        text:
          "Free to use, modify, and redistribute with attribution/license notice. The license is permissive and transparent.",
      },
      {
        title: "Official downloads are free",
        text:
          "Please avoid misleading paid repackages. If you share the app, link back to the original project and release page.",
      },
      {
        title: "Community support",
        text: "Report issues, request features, or discuss the roadmap through GitHub and Discord.",
      },
    ],
    roadmapTitle: "Roadmap",
    roadmap: [
      { title: "Update center", text: "Manual check/download/install flow is available from app settings. Silent background auto-update is not enabled." },
      {
        title: "Planned OCR Lite",
        text:
          "Scan, image, and PDF to text is planned with a low-end friendly local design. Vietnamese and English first. Not available yet.",
      },
      { title: "Installer polish", text: "Code signing and MSI/MSIX packaging are future work." },
      { title: "More PDF tools", text: "Compression, watermarking, metadata, and password features are planned." },
      { title: "More smoke coverage", text: "LibreOffice and PowerPoint real smoke tests are pending suitable local setup." },
    ],
    faqTitle: "FAQ",
    faq: [
      {
        question: "Does the website upload my files?",
        answer: "No. The website is static and has no upload form, backend, or converter service.",
      },
      {
        question: "Do I need Microsoft Office?",
        answer:
          "No for every workflow. Microsoft Office is recommended for best DOCX/PPTX fidelity. LibreOffice can be used as a fallback when installed separately.",
      },
      {
        question: "Why does Windows warn about the app?",
        answer:
          "The beta build is unsigned. Windows SmartScreen or Unknown Publisher warnings can appear until code signing is added. Verify the GitHub Release URL and SHA256 checksum before running a download.",
      },
      {
        question: "How do I verify the official download?",
        answer:
          "Download only from GitHub Releases, compare SHA256 with release notes or SHA256SUMS.txt, and avoid unofficial mirrors.",
      },
      {
        question: "Can I redistribute it?",
        answer:
          "Yes under MIT terms, with attribution/license notice. Please do not sell misleading repackages as if they were the official project.",
      },
    ],
    supportTitle: "Support and contribution",
    supportText:
      "Need help or want to contribute? Use Discord for quick discussion and GitHub for issues, source, releases, and pull requests.",
    discordCta: "Join Discord",
    footer: {
      line: "Free local Windows document converter. No upload. No telemetry.",
      license:
        "MIT licensed. Free to use, modify, and redistribute with attribution/license notice.",
    },
  },
  vi: {
    nav: {
      download: "Tải xuống",
      local: "Chạy cục bộ",
      features: "Tính năng",
      toolbox: "Công cụ PDF",
      screenshots: "Ảnh giao diện",
      roadmap: "Lộ trình",
      faq: "Hỏi đáp",
      support: "Hỗ trợ",
      source: "Mã nguồn",
    },
    controls: {
      language: "Ngôn ngữ",
      system: "Hệ thống",
      light: "Sáng",
      dark: "Tối",
    },
    hero: {
      eyebrow: "Ứng dụng Windows miễn phí và mã nguồn mở",
      title: "kmb file tools chuyển đổi tài liệu mà không tải tệp lên máy chủ.",
      subtitle:
        "Chuyển đổi tài liệu Office, PDF, hình ảnh, trang và slide cục bộ trên máy tính của bạn. Website chỉ là trang tải xuống và giới thiệu kmb file tools.",
      proof: "Chạy cục bộ · Không tải lên · Không tài khoản · Giấy phép MIT",
      installerCta: "Tải installer",
      portableCta: "Tải bản portable ZIP",
      releaseCta: "Xem GitHub Release",
      sourceCta: "Xem mã nguồn",
      releaseNote:
        "Public beta v0.1.0-beta.2. Bản chưa ký có thể bị Windows SmartScreen cảnh báo.",
    },
    download: {
      title: "Tải bản chính thức miễn phí",
      text:
        "Tải installer hoặc portable ZIP từ GitHub Releases. Bản chính thức là miễn phí. Hãy tránh các bản đóng gói bán lại gây hiểu nhầm; hãy ủng hộ dự án gốc.",
      installer: "Windows Installer EXE",
      portable: "Portable ZIP",
      allReleases: "Xem tất cả bản phát hành",
      betaNote:
        "Microsoft Office là tùy chọn để có độ chính xác DOCX/PPTX tốt nhất. LibreOffice là phương án dự phòng tùy chọn và không được đóng gói kèm.",
    },
    stats: [
      { value: "Cục bộ", label: "Xử lý trên máy" },
      { value: "Miễn phí", label: "Thân thiện cộng đồng" },
      { value: "MIT", label: "Giấy phép mã nguồn mở" },
      { value: "EN/VI", label: "Giao diện song ngữ" },
    ],
    localTitle: "Vì sao chạy cục bộ quan trọng",
    localCards: [
      {
        title: "Tệp nằm trên máy của bạn",
        text:
          "Ứng dụng desktop xử lý tài liệu cục bộ. Website không xử lý tệp của bạn và không có form upload.",
      },
      {
        title: "Không telemetry",
        text:
          "Website không có analytics, tracking pixel, crash reporter hay hệ thống tài khoản.",
      },
      {
        title: "Phụ thuộc được giải thích rõ",
        text:
          "Dùng Microsoft Office khi đã cài sẵn. Nếu không có Office, app sẽ hướng dẫn cài LibreOffice từ trang chính thức.",
      },
    ],
    featuresTitle: "Tính năng chuyển đổi tài liệu",
    featuresIntro:
      "Một công cụ Windows thực dụng cho các luồng làm việc tài liệu phổ biến, ưu tiên dễ hiểu hơn là biến thành dashboard phức tạp.",
    features: [
      {
        title: "Office sang PDF",
        text: "Chuyển DOC, DOCX, RTF, PPT và PPTX qua Microsoft Office cục bộ khi có sẵn.",
      },
      {
        title: "Hướng dẫn LibreOffice fallback",
        text: "Dùng LibreOffice headless cho các định dạng được hỗ trợ khi không có Microsoft Office.",
      },
      {
        title: "PDF sang PNG, JPEG, WebP",
        text: "Render trang PDF thật thành ảnh. Không dùng screenshot và không upload qua trình duyệt.",
      },
      {
        title: "Chuyển đổi hàng loạt",
        text: "Xếp nhiều tệp vào hàng đợi, chọn thư mục xuất và vẫn giữ kết quả khi một tệp lỗi.",
      },
      {
        title: "Trạng thái bộ chuyển đổi",
        text: "Xem Word, PowerPoint, LibreOffice và PDF renderer có sẵn hay không trước khi chạy.",
      },
      {
        title: "Tiếng Anh và tiếng Việt",
        text: "App desktop và website tự nhận ngôn ngữ hệ thống/trình duyệt, rồi nhớ lựa chọn của bạn.",
      },
    ],
    toolboxTitle: "Công cụ PDF V1",
    toolboxIntro:
      "Các tiện ích PDF cục bộ cho việc chỉnh tài liệu hằng ngày. Beta này chưa có OCR, ký số, mã hóa hay nén cloud.",
    toolbox: [
      { title: "Gộp PDF", text: "Gộp nhiều file PDF thành một file đầu ra." },
      { title: "Tách PDF", text: "Xuất từng trang thành một file PDF riêng." },
      { title: "Trích trang", text: "Tạo PDF mới từ dải trang như 1,3-5,8." },
      { title: "Xoay trang", text: "Xoay toàn bộ trang hoặc dải trang được chọn." },
      { title: "Ảnh sang PDF", text: "Tạo một PDF từ ảnh PNG, JPEG hoặc WebP." },
    ],
    screenshotsTitle: "Ảnh giao diện app",
    screenshotsIntro:
      "Ảnh và video demo ngắn cho thấy luồng website và desktop app. Website vẫn build được nếu chưa có media.",
    screenshots: [
      {
        title: "Trang chính của app",
        text: "Tệp, định dạng đích, engine, thư mục xuất và tiến trình nằm trong một trang rõ ràng.",
        src: "/demo/home-light-en.png",
      },
      {
        title: "Công cụ PDF",
        text: "Gộp, tách, trích, xoay và ảnh-sang-PDF được tách khỏi luồng chuyển đổi.",
        src: "/demo/home-light-vi.png",
      },
      {
        title: "Website dark mode",
        text: "Giao diện tối của landing page để dùng cho release note, chia sẻ và hướng dẫn người dùng.",
        src: "/demo/home-dark-en.png",
      },
    ],
    openSourceTitle: "Mã nguồn mở và cộng đồng",
    openSource: [
      {
        title: "Giấy phép MIT",
        text:
          "Miễn phí sử dụng, sửa đổi và phân phối lại nếu giữ thông tin bản quyền/giấy phép. Giấy phép rõ ràng và thoáng.",
      },
      {
        title: "Bản chính thức miễn phí",
        text:
          "Vui lòng tránh các bản đóng gói bán lại gây hiểu nhầm. Nếu chia sẻ app, hãy dẫn về dự án gốc và trang release chính thức.",
      },
      {
        title: "Hỗ trợ cộng đồng",
        text: "Báo lỗi, đề xuất tính năng hoặc thảo luận lộ trình qua GitHub và Discord.",
      },
    ],
    roadmapTitle: "Lộ trình",
    roadmap: [
      { title: "Trung tâm cập nhật", text: "Đã có luồng kiểm tra/tải/cài thủ công trong Cài đặt. Chưa có cập nhật nền tự động." },
      {
        title: "OCR Lite dự kiến",
        text:
          "Scan, ảnh và PDF sang text đã được lên kế hoạch theo hướng chạy cục bộ, nhẹ cho máy yếu. Ưu tiên tiếng Việt và tiếng Anh. Chưa có trong bản hiện tại.",
      },
      { title: "Hoàn thiện installer", text: "Code signing và MSI/MSIX là việc cho các phiên bản sau." },
      { title: "Thêm công cụ PDF", text: "Nén, watermark, metadata và mật khẩu sẽ được xem xét." },
      { title: "Thêm kiểm thử thật", text: "LibreOffice và PowerPoint real smoke còn chờ môi trường phù hợp." },
    ],
    faqTitle: "Hỏi đáp",
    faq: [
      {
        question: "Website có tải tệp của tôi lên không?",
        answer: "Không. Website là trang tĩnh, không có form upload, backend hay dịch vụ chuyển đổi.",
      },
      {
        question: "Tôi có cần Microsoft Office không?",
        answer:
          "Không phải cho mọi luồng. Microsoft Office giúp DOCX/PPTX chính xác hơn. LibreOffice có thể làm fallback khi bạn tự cài riêng.",
      },
      {
        question: "Vì sao Windows cảnh báo app?",
        answer:
          "Bản beta chưa được ký số. Windows SmartScreen hoặc Unknown Publisher có thể cảnh báo cho đến khi có code signing. Hãy kiểm tra URL GitHub Release và SHA256 trước khi chạy file.",
      },
      {
        question: "Làm sao kiểm tra bản tải chính thức?",
        answer:
          "Chỉ tải từ GitHub Releases, so SHA256 với release notes hoặc SHA256SUMS.txt, và tránh mirror không chính thức.",
      },
      {
        question: "Tôi có được phân phối lại không?",
        answer:
          "Có, theo điều khoản MIT và cần giữ thông tin bản quyền/giấy phép. Vui lòng không bán bản đóng gói gây hiểu nhầm như thể đó là dự án chính thức.",
      },
    ],
    supportTitle: "Hỗ trợ và đóng góp",
    supportText:
      "Nếu cần hỗ trợ hoặc muốn đóng góp, vào Discord để trao đổi nhanh và dùng GitHub cho issue, mã nguồn, release và pull request.",
    discordCta: "Vào Discord",
    footer: {
      line: "Công cụ chuyển đổi tài liệu Windows chạy cục bộ. Không tải lên. Không telemetry.",
      license:
        "Giấy phép MIT. Miễn phí sử dụng, sửa đổi và phân phối lại nếu giữ thông tin bản quyền/giấy phép.",
    },
  },
};
