// Form đăng nhập SSR: ẩn/hiện mật khẩu tại trình duyệt, không mở circuit hay gửi mật khẩu qua SignalR.
document.addEventListener("click", event => {
    const button = event.target.closest?.("[data-password-toggle]");
    if (!button) return;
    const input = document.getElementById(button.dataset.passwordToggle);
    if (!(input instanceof HTMLInputElement) || input.name !== "password") return;
    const visible = input.type === "password";
    input.type = visible ? "text" : "password";
    button.setAttribute("aria-pressed", String(visible));
    button.setAttribute("aria-label", visible ? "Ẩn mật khẩu" : "Hiện mật khẩu");
});
