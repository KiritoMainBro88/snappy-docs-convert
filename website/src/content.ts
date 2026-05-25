export type Language = "en" | "vi";

export type SiteCopy = {
  nav: {
    features: string;
    privacy: string;
    how: string;
    limitations: string;
    download: string;
    source: string;
  };
  hero: {
    title: string;
    subtitle: string;
    proof: string;
    primaryCta: string;
    secondaryCta: string;
    releaseNote: string;
  };
  trust: string[];
  featuresTitle: string;
  features: Array<{ title: string; text: string }>;
  privacyTitle: string;
  privacy: Array<{ title: string; text: string }>;
  howTitle: string;
  how: Array<{ title: string; text: string }>;
  limitationsTitle: string;
  limitations: string[];
  footer: {
    line: string;
    download: string;
    source: string;
  };
};

export const releasesUrl = "https://github.com/KiritoMainBro88/snappy-docs-convert/releases";
export const releaseUrl = releasesUrl;
export const sourceUrl = "https://github.com/KiritoMainBro88/snappy-docs-convert";

export const copy: Record<Language, SiteCopy> = {
  en: {
    nav: {
      features: "Features",
      privacy: "Privacy",
      how: "How it works",
      limitations: "Limitations",
      download: "Download",
      source: "Source",
    },
    hero: {
      title: "Free local document converter for Windows",
      subtitle:
        "Convert Office documents, PDFs, pages, slides, and images on your own PC. The website never processes your files.",
      proof: "No upload · No telemetry · Open source",
      primaryCta: "Download for Windows from GitHub Releases",
      secondaryCta: "View source",
      releaseNote: "Portable Windows MVP. No installer yet.",
    },
    trust: ["Local desktop app", "No account", "No backend", "No tracking"],
    featuresTitle: "Useful tools without file upload",
    features: [
      {
        title: "Office documents to PDF",
        text: "Use the local Microsoft Office engine when available, or configure LibreOffice as the fallback.",
      },
      {
        title: "PDF to PNG, JPEG, WebP",
        text: "Render real PDF pages to image files. No screenshots and no browser upload flow.",
      },
      {
        title: "Batch conversion",
        text: "Queue many files, choose one output folder, and keep partial-success results when one file fails.",
      },
      {
        title: "PDF toolbox",
        text: "Merge, split, extract, rotate, and convert images to PDF using local processing.",
      },
      {
        title: "Microsoft Office engine",
        text: "Best DOCX/PPTX fidelity when Word or PowerPoint is installed and activated in your user session.",
      },
      {
        title: "LibreOffice fallback guidance",
        text: "LibreOffice is optional and not bundled. The app guides users to install and select soffice when needed.",
      },
    ],
    privacyTitle: "Privacy by design",
    privacy: [
      {
        title: "Desktop conversion only",
        text: "Files are processed by the Windows app on your machine, not by this website.",
      },
      {
        title: "Website has no upload form",
        text: "There is no backend, no API route, and no server-side converter behind this page.",
      },
      {
        title: "No telemetry",
        text: "No analytics scripts, tracking pixels, crash reporters, or account system are included.",
      },
    ],
    howTitle: "How it works",
    how: [
      { title: "1. Download", text: "Get the portable zip from GitHub Releases." },
      { title: "2. Extract", text: "Unzip the folder anywhere you control." },
      { title: "3. Run", text: "Open SnappyDocsConvert.App.exe on Windows." },
      { title: "4. Convert", text: "Choose files, output folder, target, and convert locally." },
    ],
    limitationsTitle: "Current MVP limits",
    limitations: [
      "Windows desktop MVP only.",
      "Unsigned builds may show a Windows SmartScreen warning.",
      "LibreOffice fallback requires user-installed LibreOffice.",
      "Microsoft Office is optional, but recommended for best DOCX/PPTX fidelity.",
      "Public release depends on owner approval and GitHub Release publication.",
    ],
    footer: {
      line: "Free/community-friendly local file tools. No upload, no telemetry.",
      download: "GitHub Releases",
      source: "GitHub source",
    },
  },
  vi: {
    nav: {
      features: "Tính năng",
      privacy: "Riêng tư",
      how: "Cách dùng",
      limitations: "Giới hạn",
      download: "Tải xuống",
      source: "Mã nguồn",
    },
    hero: {
      title: "Trình chuyển đổi tài liệu cục bộ miễn phí cho Windows",
      subtitle:
        "Chuyển đổi tài liệu Office, PDF, trang, slide và hình ảnh ngay trên máy tính của bạn. Website không xử lý tệp của bạn.",
      proof: "Không tải lên · Không telemetry · Mã nguồn mở",
      primaryCta: "Tải bản Windows từ GitHub Releases",
      secondaryCta: "Xem mã nguồn",
      releaseNote: "Bản Windows portable MVP. Chưa có installer.",
    },
    trust: ["Ứng dụng desktop cục bộ", "Không cần tài khoản", "Không backend", "Không theo dõi"],
    featuresTitle: "Công cụ hữu ích, không cần upload tệp",
    features: [
      {
        title: "Office sang PDF",
        text: "Dùng Microsoft Office cục bộ khi có sẵn, hoặc cấu hình LibreOffice làm phương án dự phòng.",
      },
      {
        title: "PDF sang PNG, JPEG, WebP",
        text: "Render trang PDF thật thành ảnh. Không dùng screenshot và không upload qua trình duyệt.",
      },
      {
        title: "Chuyển đổi hàng loạt",
        text: "Xếp nhiều tệp vào hàng đợi, chọn một thư mục xuất và vẫn giữ kết quả khi một tệp lỗi.",
      },
      {
        title: "Công cụ PDF",
        text: "Gộp, tách, trích trang, xoay trang và chuyển ảnh thành PDF bằng xử lý cục bộ.",
      },
      {
        title: "Bộ chuyển đổi Microsoft Office",
        text: "Cho chất lượng DOCX/PPTX tốt nhất khi Word hoặc PowerPoint đã được cài và kích hoạt.",
      },
      {
        title: "Hướng dẫn LibreOffice",
        text: "LibreOffice là tùy chọn và không được bundle. App hướng dẫn cài và chọn soffice khi cần.",
      },
    ],
    privacyTitle: "Thiết kế ưu tiên riêng tư",
    privacy: [
      {
        title: "Chỉ chuyển đổi trên desktop",
        text: "Tệp được xử lý bởi app Windows trên máy của bạn, không phải bởi website này.",
      },
      {
        title: "Website không có form upload",
        text: "Không có backend, không có API route và không có server chuyển đổi phía sau trang này.",
      },
      {
        title: "Không telemetry",
        text: "Không analytics, tracking pixel, crash reporter hay hệ thống tài khoản.",
      },
    ],
    howTitle: "Cách dùng",
    how: [
      { title: "1. Tải xuống", text: "Tải file zip portable từ GitHub Releases." },
      { title: "2. Giải nén", text: "Giải nén vào thư mục bạn kiểm soát." },
      { title: "3. Chạy app", text: "Mở SnappyDocsConvert.App.exe trên Windows." },
      { title: "4. Chuyển đổi", text: "Chọn tệp, thư mục xuất, định dạng đích rồi chạy cục bộ." },
    ],
    limitationsTitle: "Giới hạn hiện tại của MVP",
    limitations: [
      "Hiện là bản desktop MVP cho Windows.",
      "Bản chưa ký có thể bị Windows SmartScreen cảnh báo.",
      "LibreOffice fallback yêu cầu người dùng tự cài LibreOffice.",
      "Microsoft Office là tùy chọn, nhưng nên có để DOCX/PPTX đạt chất lượng tốt nhất.",
      "Public release cần owner duyệt và tạo GitHub Release.",
    ],
    footer: {
      line: "Công cụ tệp cục bộ miễn phí cho cộng đồng. Không tải lên, không telemetry.",
      download: "GitHub Releases",
      source: "Mã nguồn GitHub",
    },
  },
};
