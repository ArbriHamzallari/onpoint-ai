/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      // ─── Typography ────────────────────────────────────────────
      fontFamily: {
        sans: [
          'Geist Sans',
          '-apple-system',
          'BlinkMacSystemFont',
          'Segoe UI',
          'Roboto',
          'sans-serif',
        ],
        display: [
          'Geist Sans',
          '-apple-system',
          'BlinkMacSystemFont',
          'sans-serif',
        ],
        mono: [
          'Geist Mono',
          'ui-monospace',
          'SFMono-Regular',
          'monospace',
        ],
      },

      // ─── Colors ────────────────────────────────────────────────
      colors: {
        // Brand: violet-blue spectrum (Review Rave inspiration)
        brand: {
          50:  '#F5F3FF',
          100: '#EDE9FE',
          200: '#DDD6FE',
          300: '#C4B5FD',
          400: '#A78BFA',
          500: '#8B5CF6',
          600: '#7C3AED',  // primary
          700: '#6D28D9',
          800: '#5B21B6',
          900: '#4C1D95',
          950: '#2E1065',
        },

        // Deep gradient anchors for guest screens
        ink: {
          900: '#0B0420',
          800: '#1A0B3D',
          700: '#2E1065',
          600: '#3B0764',
        },

        // Cool blue accent (for guest gradients + interactive states)
        azure: {
          400: '#60A5FA',
          500: '#3B82F6',
          600: '#2563EB',
          700: '#1D4ED8',
        },

        // Status / semantic
        emerald: {
          50:  '#ECFDF5',
          400: '#34D399',
          500: '#10B981',
          600: '#059669',
        },
        amber: {
          50:  '#FFFBEB',
          400: '#FBBF24',
          500: '#F59E0B',
          600: '#D97706',
        },
        rose: {
          50:  '#FFF1F2',
          400: '#FB7185',
          500: '#F43F5E',
          600: '#E11D48',
        },

        // Neutral surfaces
        surface: {
          DEFAULT: '#FAFAFC',  // staff page background
          subtle:  '#F4F4F8',
          card:    '#FFFFFF',
          glass:   'rgba(255, 255, 255, 0.6)',
        },
        ash: {
          100: '#F1F5F9',
          200: '#E2E8F0',
          300: '#CBD5E1',
          400: '#94A3B8',
          500: '#64748B',
          600: '#475569',
          700: '#334155',
          800: '#1E293B',
          900: '#0F172A',
        },

        // Legacy aliases (so unmodified screens keep compiling
        // until they're restyled in Phase 2.5b)
        primary: {
          DEFAULT: '#7C3AED',
          50:  '#F5F3FF',
          100: '#EDE9FE',
          500: '#8B5CF6',
          600: '#7C3AED',
          700: '#6D28D9',
        },
        success: '#10B981',
        warning: '#F59E0B',
        danger:  '#F43F5E',
        card:    '#FFFFFF',
        border:  '#E2E8F0',
        text: {
          primary:   '#0F172A',
          secondary: '#64748B',
        },
      },

      // ─── Shadows: layered, soft, premium ───────────────────────
      boxShadow: {
        // Multi-layer ambient shadows
        'soft':  '0 1px 2px rgba(15, 23, 42, 0.04), 0 1px 3px rgba(15, 23, 42, 0.06)',
        'card':  '0 2px 4px rgba(15, 23, 42, 0.04), 0 4px 12px rgba(15, 23, 42, 0.06)',
        'lift':  '0 4px 8px rgba(15, 23, 42, 0.06), 0 12px 24px rgba(15, 23, 42, 0.08)',
        'float': '0 8px 16px rgba(15, 23, 42, 0.08), 0 24px 48px rgba(15, 23, 42, 0.12)',

        // Brand glow for buttons / active states
        'glow':       '0 0 0 1px rgba(124, 58, 237, 0.12), 0 4px 16px rgba(124, 58, 237, 0.24)',
        'glow-lg':    '0 0 0 1px rgba(124, 58, 237, 0.16), 0 8px 32px rgba(124, 58, 237, 0.32)',
        'glow-azure': '0 0 0 1px rgba(59, 130, 246, 0.16), 0 8px 32px rgba(59, 130, 246, 0.32)',

        // Inner shadow for press states
        'inner-soft': 'inset 0 1px 2px rgba(15, 23, 42, 0.06)',
      },

      // ─── Border radius: rounded everywhere ─────────────────────
      borderRadius: {
        DEFAULT: '10px',
        sm:  '6px',
        md:  '10px',
        lg:  '14px',
        xl:  '20px',
        '2xl': '28px',
        '3xl': '36px',
      },

      // ─── Background images for gradient surfaces ───────────────
      backgroundImage: {
        // Guest hero gradients
        'guest-hero':   'radial-gradient(ellipse at top left, #3B0764 0%, #1A0B3D 40%, #0B0420 100%)',
        'guest-warm':   'linear-gradient(135deg, #7C3AED 0%, #3B82F6 100%)',
        'guest-aurora': 'linear-gradient(135deg, #2E1065 0%, #6D28D9 35%, #3B82F6 70%, #60A5FA 100%)',

        // Staff card subtle gradients
        'card-subtle':  'linear-gradient(180deg, #FFFFFF 0%, #FAFAFC 100%)',
        'card-brand':   'linear-gradient(135deg, #F5F3FF 0%, #EDE9FE 100%)',

        // Brand gradient for buttons / accents
        'brand-fill':   'linear-gradient(135deg, #7C3AED 0%, #5B21B6 100%)',
        'brand-glow':   'linear-gradient(135deg, #8B5CF6 0%, #6D28D9 100%)',
      },

      // ─── Animations ────────────────────────────────────────────
      keyframes: {
        'fade-in': {
          '0%':   { opacity: '0' },
          '100%': { opacity: '1' },
        },
        'fade-up': {
          '0%':   { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'pulse-ring': {
          '0%':   { transform: 'scale(0.95)', opacity: '0.6' },
          '70%':  { transform: 'scale(1.4)',  opacity: '0' },
          '100%': { transform: 'scale(1.4)',  opacity: '0' },
        },
        'pulse-soft': {
          '0%, 100%': { opacity: '1' },
          '50%':      { opacity: '0.5' },
        },
        'shimmer': {
          '0%':   { backgroundPosition: '-200% 0' },
          '100%': { backgroundPosition: '200% 0' },
        },
      },
      animation: {
        'fade-in':    'fade-in 200ms ease-out',
        'fade-up':    'fade-up 280ms cubic-bezier(0.16, 1, 0.3, 1)',
        'pulse-ring': 'pulse-ring 1.8s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'pulse-soft': 'pulse-soft 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'shimmer':    'shimmer 1.6s linear infinite',
      },
    },
  },
  plugins: [],
}
