import axios from "axios";

const API_URL = "http://localhost:5018/api";

export const register = async (registerData) => {
  const response = await axios.post(
    `${API_URL}/Auth/register`,
    registerData
  );

  return response.data;
};