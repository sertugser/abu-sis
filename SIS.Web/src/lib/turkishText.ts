const TURKISH_TEXT_REPLACEMENTS: Array<[RegExp, string]> = [
  [/\bGuz\b/g, 'G\u00fcz'],
  [/\bOn Lisans\b/g, '\u00d6n Lisans'],
  [/\bOgrenci\b/g, '\u00d6\u011frenci'],
  [/\bOgretimi\b/g, '\u00d6\u011fretimi'],
  [/\bOgretim\b/g, '\u00d6\u011fretim'],
  [/\bOgrenim\b/g, '\u00d6\u011frenim'],
  [/\bOdemeleri\b/g, '\u00d6demeleri'],
  [/\bOdemeler\b/g, '\u00d6demeler'],
  [/\bOdeme\b/g, '\u00d6deme'],
  [/\bOdenmesi\b/g, '\u00d6denmesi'],
  [/\bUcretlerinin\b/g, '\u00dccretlerinin'],
  [/\bUcret\b/g, '\u00dccret'],
  [/\bBaslamasi\b/g, 'Ba\u015flamas\u0131'],
  [/\bBaslangici\b/g, 'Ba\u015flang\u0131c\u0131'],
  [/\bBaslangic\b/g, 'Ba\u015flang\u0131\u00e7'],
  [/\bBitisi\b/g, 'Biti\u015fi'],
  [/\bBitis\b/g, 'Biti\u015f'],
  [/\bBasvurularinin\b/g, 'Ba\u015fvurular\u0131n\u0131n'],
  [/\bBasvurulari\b/g, 'Ba\u015fvurular\u0131'],
  [/\bBasvurusu\b/g, 'Ba\u015fvurusu'],
  [/\bBasvuru\b/g, 'Ba\u015fvuru'],
  [/\bKayitlari\b/g, 'Kay\u0131tlar\u0131'],
  [/\bKayit\b/g, 'Kay\u0131t'],
  [/\bSinavlarinin\b/g, 'S\u0131navlar\u0131n\u0131n'],
  [/\bSinavlari\b/g, 'S\u0131navlar\u0131'],
  [/\bSinavlar\b/g, 'S\u0131navlar'],
  [/\bSinavi\b/g, 'S\u0131nav\u0131'],
  [/\bSinav\b/g, 'S\u0131nav'],
  [/\bSonuclarinin\b/g, 'Sonu\u00e7lar\u0131n\u0131n'],
  [/\bSonuclari\b/g, 'Sonu\u00e7lar\u0131'],
  [/\bSonucu\b/g, 'Sonucu'],
  [/\bSonuc\b/g, 'Sonu\u00e7'],
  [/\bIlaninin\b/g, '\u0130lan\u0131n\u0131n'],
  [/\bIlani\b/g, '\u0130lan\u0131'],
  [/\bIlan\b/g, '\u0130lan'],
  [/\bDonem\b/g, 'D\u00f6nem'],
  [/\bYariyil\b/g, 'Yar\u0131y\u0131l'],
  [/\bGec\b/g, 'Ge\u00e7'],
  [/\bGunu\b/g, 'G\u00fcn\u00fc'],
  [/\bDanisman\b/g, 'Dan\u0131\u015fman'],
  [/\bCekilme\b/g, '\u00c7ekilme'],
  [/\bBirakma\b/g, 'B\u0131rakma'],
  [/\bBirak\b/g, 'B\u0131rak'],
  [/\bCift\b/g, '\u00c7ift'],
  [/\bDegerlendirilmesi\b/g, 'De\u011ferlendirilmesi'],
  [/\bDegerlendirme\b/g, 'De\u011ferlendirme'],
  [/\bGecis\b/g, 'Ge\u00e7i\u015f'],
  [/\bLisansustu\b/g, 'Lisans\u00fcst\u00fc'],
  [/\bHazirlik\b/g, 'Haz\u0131rl\u0131k'],
  [/\bIngilizce\b/g, '\u0130ngilizce'],
  [/\bIcin\b/g, '\u0130\u00e7in'],
  [/\bicin\b/g, 'i\u00e7in'],
  [/\bEgitimi\b/g, 'E\u011fitimi'],
  [/\bAciklanmasi\b/g, 'A\u00e7\u0131klanmas\u0131'],
  [/\bAlinmasi\b/g, 'Al\u0131nmas\u0131'],
  [/\bIadelerinin\b/g, '\u0130adelerinin'],
  [/\bYapilmasi\b/g, 'Yap\u0131lmas\u0131'],
  [/\bProgramlarina\b/g, 'Programlar\u0131na'],
  [/\bProgramlari\b/g, 'Programlar\u0131'],
  [/\bProgrami\b/g, 'Program\u0131'],
  [/\bUluslararasi\b/g, 'Uluslararas\u0131'],
  [/\bYilbasi\b/g, 'Y\u0131lba\u015f\u0131'],
  [/\bCocuk\b/g, '\u00c7ocuk'],
  [/\bDayanisma\b/g, 'Dayan\u0131\u015fma'],
  [/\bAtaturk\b/g, 'Atat\u00fcrk'],
  [/\bGenclik\b/g, 'Gen\u00e7lik'],
  [/\bBirlik\b/g, 'Birlik'],
  [/\bBayrami\b/g, 'Bayram\u0131'],
];

function decodeUtf8Mojibake(value: string) {
  if (!/[ÃÄÅÂ]/.test(value))
    return value;

  try {
    const bytes = Uint8Array.from(Array.from(value), char => {
      const code = char.charCodeAt(0);
      if (code > 0xff)
        throw new Error('Non-Latin-1 character encountered');
      return code;
    });

    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return value;
  }
}

export function displayTurkishText(value?: string | null) {
  if (!value)
    return '';

  const normalized = decodeUtf8Mojibake(value);
  return TURKISH_TEXT_REPLACEMENTS.reduce((text, [pattern, replacement]) => text.replace(pattern, replacement), normalized);
}
