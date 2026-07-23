import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';

// Finara marka kimliği — Aura'nın üzerine kurulu ince bir katman, yeni bir tema
// kütüphanesi değil. Üç isimli palet:
// - "pine": marka rengi, koyu bir defter-mürekkebi çam-yeşili teal (buton/link/odak halkası).
// - "amber": eski defter altın yaldızı — PrimeNG'in "primary" sistemine hiç bağlanmıyor,
//   sadece KPI/vurgu anlarında elle (--p-amber-500 vb.) kullanılan ikincil bir aksan.
// - "stone": varsayılan Aura'nın "slate" (mavi-gri) nötrü yerine, çam yeşiline hafif
//   kaymış soğuk-sıcak bir nötr — "seçilmiş" hissettiren bir gri, ham/varsayılan değil.
export const FinaraPreset = definePreset(Aura, {
  primitive: {
    borderRadius: {
      none: '0',
      xs: '4px',
      sm: '6px',
      md: '10px',
      lg: '14px',
      xl: '20px'
    },
    pine: {
      50: '#EEF7F4',
      100: '#D7ECE5',
      200: '#AEDACD',
      300: '#7EC0AD',
      400: '#4FA189',
      500: '#227E67',
      600: '#1B6553',
      700: '#175243',
      800: '#134338',
      900: '#0F332B',
      950: '#081E19'
    },
    amber: {
      50: '#FBF6ED',
      100: '#F5E8D0',
      200: '#EACC9E',
      300: '#DDA968',
      400: '#CC8A3F',
      500: '#B8752C',
      600: '#955D22',
      700: '#74481D',
      800: '#543419',
      900: '#382310',
      950: '#1F130A'
    },
    stone: {
      0: '#FFFFFF',
      50: '#F6F8F7',
      100: '#ECF0EE',
      200: '#DCE3E0',
      300: '#C2CCC7',
      400: '#93A19A',
      500: '#6C7B74',
      600: '#52605A',
      700: '#404B46',
      800: '#2C3532',
      900: '#1B211F',
      950: '#0E1211'
    }
  },
  semantic: {
    primary: {
      50: '{pine.50}',
      100: '{pine.100}',
      200: '{pine.200}',
      300: '{pine.300}',
      400: '{pine.400}',
      500: '{pine.500}',
      600: '{pine.600}',
      700: '{pine.700}',
      800: '{pine.800}',
      900: '{pine.900}',
      950: '{pine.950}'
    },
    colorScheme: {
      light: {
        surface: {
          0: '{stone.0}',
          50: '{stone.50}',
          100: '{stone.100}',
          200: '{stone.200}',
          300: '{stone.300}',
          400: '{stone.400}',
          500: '{stone.500}',
          600: '{stone.600}',
          700: '{stone.700}',
          800: '{stone.800}',
          900: '{stone.900}',
          950: '{stone.950}'
        }
      },
      // ÖNEMLİ — sadece "surface" vermek yetmiyor: node_modules/@primeng/themes/aura/base
      // kaynağından doğrulandı (canlı DOM'da --p-content-background/--p-dialog-background
      // vb. karanlık modda hâlâ AÇIK değerde kaldığı görülünce) — definePreset burada
      // Aura'nın TÜM "dark" nesnesini (content/text/formField/overlay/list/navigation/
      // primary/highlight/mask) sessizce siler, sadece verdiğimiz "surface" kalır. Bu
      // yüzden Aura'nın dark bloğu BÜTÜNÜYLE buraya kopyalandı — "surface" TERS çevrilmiş
      // stone skalasına bağlandı (0 en koyu zemin, 950 en açık uç — PrimeNG'in dark
      // colorScheme'lerde beklediği standart desen), geri kalanı (form alanı/liste/nav
      // renkleri) Aura'nın kendi jenerik mekaniği olduğu için aynen bırakıldı.
      // "surface" TERS çevrilmiş (0 en koyu, 950 en açık — bkz. yukarıdaki not) — Aura'nın
      // kendi dark bloğundaki (base/index.mjs) tüm index referansları bu yüzden BİREBİR
      // kopyalanamadı, ters çevrimin simetrik eşiyle (0↔950, 50↔900, 100↔800, 200↔700,
      // 300↔600, 400↔500) yeniden eşlendi — ör. Aura "content.background: {surface.900}"
      // (kendi düz skalasında koyu bir ton) diyordu, bizim ters skalamızda aynı koyuluk
      // "{surface.50}" karşılığı. Bu satırları değiştirirken bu eşleme unutulmamalı.
      dark: {
        surface: {
          0: '{stone.950}',
          50: '{stone.900}',
          100: '{stone.800}',
          200: '{stone.700}',
          300: '{stone.600}',
          400: '{stone.500}',
          500: '{stone.400}',
          600: '{stone.300}',
          700: '{stone.200}',
          800: '{stone.100}',
          900: '{stone.50}',
          950: '{stone.0}'
        },
        primary: {
          color: '{primary.400}',
          contrastColor: '{surface.50}',
          hoverColor: '{primary.300}',
          activeColor: '{primary.200}'
        },
        highlight: {
          background: 'color-mix(in srgb, {primary.400}, transparent 84%)',
          focusBackground: 'color-mix(in srgb, {primary.400}, transparent 76%)',
          color: 'rgba(255,255,255,.87)',
          focusColor: 'rgba(255,255,255,.87)'
        },
        mask: {
          background: 'rgba(0,0,0,0.6)',
          color: '{surface.700}'
        },
        formField: {
          background: '{surface.0}',
          disabledBackground: '{surface.200}',
          filledBackground: '{surface.100}',
          filledHoverBackground: '{surface.100}',
          filledFocusBackground: '{surface.100}',
          borderColor: '{surface.300}',
          hoverBorderColor: '{surface.400}',
          focusBorderColor: '{primary.color}',
          invalidBorderColor: '{red.300}',
          color: '{surface.950}',
          disabledColor: '{surface.500}',
          placeholderColor: '{surface.500}',
          invalidPlaceholderColor: '{red.400}',
          floatLabelColor: '{surface.500}',
          floatLabelFocusColor: '{primary.color}',
          floatLabelActiveColor: '{surface.500}',
          floatLabelInvalidColor: '{form.field.invalid.placeholder.color}',
          iconColor: '{surface.500}',
          shadow: '0 0 #0000, 0 0 #0000, 0 1px 2px 0 rgba(18, 18, 23, 0.05)'
        },
        text: {
          color: '{surface.950}',
          hoverColor: '{surface.950}',
          mutedColor: '{surface.500}',
          hoverMutedColor: '{surface.600}'
        },
        content: {
          background: '{surface.50}',
          hoverBackground: '{surface.100}',
          borderColor: '{surface.200}',
          color: '{text.color}',
          hoverColor: '{text.hover.color}'
        },
        overlay: {
          select: {
            background: '{surface.50}',
            borderColor: '{surface.200}',
            color: '{text.color}'
          },
          popover: {
            background: '{surface.50}',
            borderColor: '{surface.200}',
            color: '{text.color}'
          },
          modal: {
            background: '{surface.50}',
            borderColor: '{surface.200}',
            color: '{text.color}'
          }
        },
        list: {
          option: {
            focusBackground: '{surface.100}',
            selectedBackground: '{highlight.background}',
            selectedFocusBackground: '{highlight.focus.background}',
            color: '{text.color}',
            focusColor: '{text.hover.color}',
            selectedColor: '{highlight.color}',
            selectedFocusColor: '{highlight.focus.color}',
            icon: {
              color: '{surface.400}',
              focusColor: '{surface.500}'
            }
          },
          optionGroup: {
            background: 'transparent',
            color: '{text.muted.color}'
          }
        },
        navigation: {
          item: {
            focusBackground: '{surface.100}',
            activeBackground: '{surface.100}',
            color: '{text.color}',
            focusColor: '{text.hover.color}',
            activeColor: '{text.hover.color}',
            icon: {
              color: '{surface.400}',
              focusColor: '{surface.500}',
              activeColor: '{surface.500}'
            }
          },
          submenuLabel: {
            background: 'transparent',
            color: '{text.muted.color}'
          },
          submenuIcon: {
            color: '{surface.400}',
            focusColor: '{surface.500}',
            activeColor: '{surface.500}'
          }
        }
      }
    }
  },
  components: {
    card: {
      root: {
        borderRadius: '{border.radius.lg}',
        // Pine-950'nin rgb'sine yakın bir gölge tonu — jenerik siyah gölge yerine
        // markanın kendi rengine kaymış, "seçilmiş" bir ambient gölge.
        shadow: '0 1px 2px 0 rgba(8, 30, 25, 0.04), 0 12px 28px -10px rgba(8, 30, 25, 0.12)'
      },
      body: {
        padding: '1.5rem',
        gap: '0.625rem'
      }
    },
    // Nav bar artık markanın en cüretkar anı — koyu pine, tüm ekranlarda sabit,
    // beyaz sayfanın içinde "yüzen" jenerik bir çubuk olmak yerine kendi kimliğini
    // taşıyan bir şerit. .app-shell-* sınıfları (styles.scss) bu koyu zemine göre
    // ayrıca ayarlandı (metin/aktif durum/rozet renkleri).
    menubar: {
      root: {
        borderRadius: '0',
        gap: '0.5rem'
      },
      // Nav bar açık/karanlık modda AYNI kalıyor (her zaman koyu pine) — bu yüzden
      // dark bloğu light'ın birebir kopyası, mode'a göre değişmiyor.
      colorScheme: {
        light: {
          root: {
            background: 'linear-gradient(120deg, #0D2A23 0%, #175243 100%)',
            borderColor: 'transparent',
            color: '{stone.100}'
          }
        },
        dark: {
          root: {
            background: 'linear-gradient(120deg, #0D2A23 0%, #175243 100%)',
            borderColor: 'transparent',
            color: '{stone.100}'
          }
        }
      }
    },
    button: {
      // Aynı gerekçe — primary buton gradyanı da mode'dan bağımsız sabit.
      colorScheme: {
        light: {
          root: {
            primary: {
              // Düz primary.color yerine hafif bir gradyan + kenarlıksız dolgu —
              // butonlar artık ışık alan, "tıklanası" bir yüzey hissi veriyor.
              background: 'linear-gradient(135deg, #2A9179 0%, #175243 100%)',
              hoverBackground: 'linear-gradient(135deg, #227E67 0%, #134338 100%)',
              activeBackground: '{pine.800}',
              borderColor: 'transparent',
              hoverBorderColor: 'transparent',
              activeBorderColor: 'transparent'
            }
          }
        },
        dark: {
          root: {
            primary: {
              background: 'linear-gradient(135deg, #2A9179 0%, #175243 100%)',
              hoverBackground: 'linear-gradient(135deg, #227E67 0%, #134338 100%)',
              activeBackground: '{pine.800}',
              borderColor: 'transparent',
              hoverBorderColor: 'transparent',
              activeBorderColor: 'transparent'
            }
          }
        }
      }
    }
  }
});
