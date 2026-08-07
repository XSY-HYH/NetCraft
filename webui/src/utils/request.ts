import axios from 'axios';

import key from '@/const/key';

export const serverRequest = axios.create({
  timeout: 30000, // 30秒，获取版本列表可能较慢
});

export const request = axios.create({
  timeout: 10000,
});

export const requestServerWithFetch = async (
  url: string,
  options: RequestInit
) => {
  const token = localStorage.getItem(key.token);

  if (token) {
    options.headers = {
      ...options.headers,
      Authorization: `Bearer ${JSON.parse(token)}`,
    };
  }

  const baseURL = '/api';

  const response = await fetch(baseURL + url, options);

  return response;
};

serverRequest.interceptors.request.use((config) => {
  const baseURL = '/api';

  config.baseURL = baseURL;

  const token = localStorage.getItem(key.token);

  if (token) {
    config.headers['Authorization'] = `Bearer ${JSON.parse(token)}`;
  }

  return config;
});

serverRequest.interceptors.response.use((response) => {
  // 流式文件下载（blob）：直接放行，不解析 code
  if (response.data instanceof Blob) {
    return response;
  }
  if (response.data.code !== 0) {
    if (response.data.message === 'Unauthorized') {
      const token = localStorage.getItem(key.token);
      if (token && JSON.parse(token)) {
        localStorage.removeItem(key.token);
        window.location.reload();
      }
    }
    throw new Error(response.data.message);
  }

  return response;
});
