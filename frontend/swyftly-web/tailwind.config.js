/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        swyftly: {
          primary: '#3A1D32',
          primaryHover: '#2A1425',
          accent: '#B76E79',
          background: '#FFF9F4',
          surface: '#FFFFFF',
          text: '#1F1A1C',
          muted: '#6F5E66'
        }
      }
    }
  },
  plugins: []
};
