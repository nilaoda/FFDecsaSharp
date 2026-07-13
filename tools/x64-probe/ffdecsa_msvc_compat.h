#ifndef FFDECSA_MSVC_COMPAT_H
#define FFDECSA_MSVC_COMPAT_H

/* FFdecsa uses GCC's always-inline attribute in two transpose helpers. */
#ifndef __attribute__
#define __attribute__(x)
#endif

#endif
