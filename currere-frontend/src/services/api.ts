import axios from 'axios';
import { useAuthStore } from '../store/useAuthStore';

// Create an Axios instance
const api = axios.create({
  baseURL: 'http://localhost:5279/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to attach JWT token
api.interceptors.request.use(
  (config) => {
    const token = useAuthStore.getState().token;
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

export default api;
