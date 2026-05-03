/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: '#2563EB',
          50:  '#EFF6FF',
          100: '#DBEAFE',
          500: '#3B82F6',
          600: '#2563EB',
          700: '#1D4ED8',
        },
        success: '#22C55E',
        warning: '#EAB308',
        danger:  '#EF4444',
        surface: '#F8FAFC',
        card:    '#FFFFFF',
        border:  '#E2E8F0',
        text: {
          primary:   '#1E293B',
          secondary: '#64748B',
        },
      },
      borderRadius: {
        DEFAULT: '8px',
        lg: '12px',
      },
    },
  },
  plugins: [],
}
