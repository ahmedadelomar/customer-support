/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        // Brand palette. Runtime branding overrides these through CSS custom properties,
        // so components reference `brand-*` rather than hard-coded hex values.
        brand: {
          50: 'var(--brand-50, #F5F3FF)',
          100: 'var(--brand-100, #EDE9FE)',
          500: 'var(--brand-500, #7C3AED)',
          600: 'var(--brand-600, #6D28D9)',
          700: 'var(--brand-700, #5B2C8D)',
          900: 'var(--brand-900, #3B1E5C)',
        },
        accent: {
          50: 'var(--accent-50, #ECFEFF)',
          600: 'var(--accent-600, #0E7490)',
        },
      },
      fontFamily: {
        sans: ['Inter', 'Tajawal', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
