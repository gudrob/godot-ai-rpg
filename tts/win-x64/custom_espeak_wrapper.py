import logging
import re
import subprocess
from typing import Dict, List

from TTS.tts.utils.text.phonemizers.base import BasePhonemizer
from TTS.tts.utils.text.punctuation import Punctuation

_ESPEAK_LIB = "espeak"

def _espeak_exe(args: List, sync=False) -> List[str]:
    """Run espeak with the given arguments."""
    cmd = [
        "./espeak",
        "--path=./",
        "-q",
        "-b",
        "1",  # UTF8 text encoding
    ]

    cmd.extend(args)

    with subprocess.Popen(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    ) as p:
        res = iter(p.stdout.readline, b"")

        if not sync:
            p.stdout.close()
            if p.stderr:
                p.stderr.close()
            if p.stdin:
                p.stdin.close()
            return res
        res2 = []
        for line in res:
            res2.append(line)
            print(line)
        p.stdout.close()
        if p.stderr:
            p.stderr.close()
        if p.stdin:
            p.stdin.close()
        p.wait()
    return res2

class ESpeak(BasePhonemizer):

    def __init__(self, language: str, backend=None, punctuations=Punctuation.default_puncs(), keep_puncs=True):
        backend = self.backend = _ESPEAK_LIB

        # band-aid for backwards compatibility
        if language == "en":
            language = "en-us"
        if language == "zh-cn":
            language = "cmn"

        super().__init__(language, punctuations=punctuations, keep_puncs=keep_puncs)
        if backend is not None:
            self.backend = backend

    @property
    def backend(self):
        return self._ESPEAK_LIB

    @property
    def backend_version(self):
        return self._ESPEAK_VER

    @backend.setter
    def backend(self, backend):
        self._ESPEAK_LIB = backend

    @staticmethod
    def name():
        return _ESPEAK_LIB

    def phonemize_espeak(self, text: str, separator: str = "|", tie=False) -> str:
        """Convert input text to phonemes.

        Args:
            text (str):
                Text to be converted to phonemes.

            tie (bool, optional) : When True use a '͡' character between
                consecutive characters of a single phoneme. Else separate phoneme
                with '_'. This option requires espeak>=1.49. Default to False.
        """
        # set arguments
        args = ["-v", f"{self._language}"]
        # espeak and espeak-ng parses `ipa` differently
        if tie:
            # use '͡' between phonemes
            if self.backend == "espeak":
                args.append("--ipa=1")
            else:
                args.append("--ipa=3")
        else:
            # split with '_'
            if self.backend == "espeak":
                args.append("--ipa=3")
            else:
                args.append("--ipa=1")
        if tie:
            args.append("--tie=%s" % tie)

        args.append(text)
        # compute phonemes
        phonemes = ""
        for line in _espeak_exe(args, sync=True):
            logging.debug("line: %s", repr(line))
            ph_decoded = line.decode("utf8").strip()
            # espeak:
            #   version 1.48.15: " p_ɹ_ˈaɪ_ɚ t_ə n_oʊ_v_ˈɛ_m_b_ɚ t_w_ˈɛ_n_t_i t_ˈuː\n"
            # espeak-ng:
            #   "p_ɹ_ˈaɪ_ɚ t_ə n_oʊ_v_ˈɛ_m_b_ɚ t_w_ˈɛ_n_t_i t_ˈuː\n"

            # espeak-ng backend can add language flags that need to be removed:
            #   "sɛʁtˈɛ̃ mˈo kɔm (en)fˈʊtbɔːl(fr) ʒenˈɛʁ de- flˈaɡ də- lˈɑ̃ɡ."
            # phonemize needs to remove the language flags of the returned text:
            #   "sɛʁtˈɛ̃ mˈo kɔm fˈʊtbɔːl ʒenˈɛʁ de- flˈaɡ də- lˈɑ̃ɡ."
            ph_decoded = re.sub(r"\(.+?\)", "", ph_decoded)

            phonemes += ph_decoded.strip()
        return phonemes.replace("_", separator)

    def _phonemize(self, text, separator=None):
        return self.phonemize_espeak(text, separator, tie=False)

    @staticmethod
    def supported_languages() -> Dict:
        """Get a dictionary of supported languages.

        Returns:
            Dict: Dictionary of language codes.
        """
        args = ["--voices"]
        langs = {}
        count = 0
        for line in _espeak_exe(args, sync=True):
            line = line.decode("utf8").strip()
            if count > 0:
                cols = line.split()
                lang_code = cols[1]
                lang_name = cols[3]
                langs[lang_code] = lang_name
            logging.debug("line: %s", repr(line))
            count += 1
        return langs

    def version(self) -> str:
        return ""

    @classmethod
    def is_available(cls):
        return True