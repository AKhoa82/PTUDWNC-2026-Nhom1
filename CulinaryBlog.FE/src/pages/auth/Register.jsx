import { useState } from "react";
import { register } from "../../services/authService";
import "./Register.css";

function Register() {
  const [showPassword, setShowPassword] = useState(false);

  const [formData, setFormData] = useState({
    fullName: "",
    email: "",
    password: "",
  });

  const [loading, setLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const handleChange = (e) => {
    const { name, value } = e.target;

    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));

    setErrorMessage("");
    setSuccessMessage("");
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    setErrorMessage("");
    setSuccessMessage("");

    if (formData.password.length < 6) {
      setErrorMessage("Mật khẩu phải có ít nhất 6 ký tự.");
      return;
    }

    try {
      setLoading(true);

      const data = await register(formData);

      console.log("Register success:", data);

      setSuccessMessage("Đăng ký tài khoản thành công!");

      setFormData({
        fullName: "",
        email: "",
        password: "",
      });
    } catch (error) {
      console.error("Register error:", error);

      if (error.response?.status === 409) {
        setErrorMessage(
          error.response.data?.message ||
            "Email này đã được sử dụng."
        );
      } else if (error.response?.data?.message) {
        setErrorMessage(error.response.data.message);
      } else {
        setErrorMessage(
          "Không thể kết nối đến máy chủ. Vui lòng thử lại."
        );
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="register-page">
      <div className="register-card">
        {/* Logo */}
        <div className="brand">
          <div className="brand-icon">🍳</div>
          <h1>CulinaryBlog</h1>
        </div>

        {/* Header */}
        <div className="register-header">
          <h2>Tạo tài khoản</h2>

          <p>
            Tham gia cộng đồng yêu ẩm thực và chia sẻ những công thức
            tuyệt vời của bạn.
          </p>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="register-form">
          {/* Full Name */}
          <div className="form-group">
            <label htmlFor="fullName">Họ và tên</label>

            <div className="input-wrapper">
              <span className="input-icon">👤</span>

              <input
                id="fullName"
                type="text"
                name="fullName"
                placeholder="Nhập họ và tên"
                value={formData.fullName}
                onChange={handleChange}
                required
              />
            </div>
          </div>

          {/* Email */}
          <div className="form-group">
            <label htmlFor="email">Email</label>

            <div className="input-wrapper">
              <span className="input-icon">✉️</span>

              <input
                id="email"
                type="email"
                name="email"
                placeholder="Nhập địa chỉ email"
                value={formData.email}
                onChange={handleChange}
                required
              />
            </div>
          </div>

          {/* Password */}
          <div className="form-group">
            <label htmlFor="password">Mật khẩu</label>

            <div className="input-wrapper">
              <span className="input-icon">🔒</span>

              <input
                id="password"
                type={showPassword ? "text" : "password"}
                name="password"
                placeholder="Nhập mật khẩu"
                value={formData.password}
                onChange={handleChange}
                required
                minLength={6}
              />

              <button
                type="button"
                className="password-toggle"
                onClick={() => setShowPassword((prev) => !prev)}
              >
                {showPassword ? "🙈" : "👁️"}
              </button>
            </div>
          </div>

          {/* Error */}
          {errorMessage && (
            <div className="message error-message">
              {errorMessage}
            </div>
          )}

          {/* Success */}
          {successMessage && (
            <div className="message success-message">
              {successMessage}
            </div>
          )}

          {/* Submit */}
          <button
            type="submit"
            className="register-button"
            disabled={loading}
          >
            {loading ? "Đang tạo tài khoản..." : "Tạo tài khoản"}
          </button>
        </form>

        {/* Login */}
        <div className="login-link">
          <span>Đã có tài khoản?</span>

          <button type="button">
            Đăng nhập
          </button>
        </div>
      </div>

      {/* Right side */}
      <div className="register-decoration">
        <div className="decoration-content">
          <span className="decoration-icon">🥗</span>

          <h2>Khám phá thế giới ẩm thực</h2>

          <p>
            Lưu lại những công thức yêu thích, chia sẻ món ăn của bạn
            và cùng khám phá những hương vị mới.
          </p>
        </div>
      </div>
    </div>
  );
}

export default Register;